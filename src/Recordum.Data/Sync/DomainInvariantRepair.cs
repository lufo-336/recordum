using System.Data.Common;
using System.Text.Json;
using Recordum.Core.Abstractions;

namespace Recordum.Data.Sync;

/// <summary>
/// Ripara le invarianti di dominio che due modifiche concorrenti su righe diverse possono violare
/// (spec §8.3, <c>docs/formats/sync-file.md</c> §6.7):
/// <list type="bullet">
/// <item>Due periodi di astinenza aperti sulla stessa abitudine: resta aperto quello iniziato prima, gli altri si
/// chiudono al proprio inizio (<c>ended_at_ms = started_at_ms</c>, durata zero).</item>
/// <item>Ciclo in <c>habit_nodes.parent_id</c>: perde lo spostamento più recente (ora del campo <c>parent_id</c>
/// maggiore, a parità GUID maggiore) e torna alla madre di prima letta da <c>sync_base</c> se esiste ancora e non crea
/// cicli, altrimenti in radice.</item>
/// </list>
/// </summary>
public static class DomainInvariantRepair
{
    private static readonly HashSet<string> EndedAtFields = new(StringComparer.Ordinal) { "ended_at_ms" };
    private static readonly HashSet<string> ParentIdFields = new(StringComparer.Ordinal) { "parent_id" };

    public static Task<int> RepairAsync(DbConnection conn, IClock clock, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(clock);
        return RepairAsync(conn, tx: null, clock, ct);
    }

#pragma warning disable CA2100 // SQL parametrico su tabelle e colonne fisse dello schema
    internal static async Task<int> RepairAsync(
        DbConnection conn, DbTransaction? tx, IClock clock, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(clock);

        var repaired = 0;
        repaired += await RepairOpenAbstinencePeriodsAsync(conn, tx, clock, ct).ConfigureAwait(false);
        repaired += await RepairParentCyclesAsync(conn, tx, clock, ct).ConfigureAwait(false);
        return repaired;
    }

    private static async Task<int> RepairOpenAbstinencePeriodsAsync(
        DbConnection conn, DbTransaction? tx, IClock clock, CancellationToken ct)
    {
        var openByHabit = new Dictionary<string, List<OpenPeriod>>(StringComparer.Ordinal);
        await using (var cmd = Command(conn, tx,
            "SELECT \"id\", \"habit_node_id\", \"started_at_ms\", \"updated_at_ms\" " +
            "FROM \"abstinence_period\" WHERE \"ended_at_ms\" IS NULL " +
            "ORDER BY \"started_at_ms\" ASC, \"id\" ASC"))
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var habitId = reader.GetString(1);
                var startedAtMs = reader.GetInt64(2);
                var updatedAtMs = reader.GetInt64(3);
                if (!openByHabit.TryGetValue(habitId, out var list))
                {
                    list = [];
                    openByHabit[habitId] = list;
                }
                list.Add(new OpenPeriod(id, startedAtMs, updatedAtMs));
            }
        }

        var repaired = 0;
        foreach (var periods in openByHabit.Values.Where(l => l.Count > 1))
        {
            var maxUpdatedMs = periods.Max(p => p.UpdatedAtMs);
            var repairMs = Math.Max(clock.UtcNowMs, maxUpdatedMs + 1);

            foreach (var late in periods.Skip(1))
            {
                // Mantiene l'ora precedente sugli altri campi così una modifica concorrente altrove non perde.
                await SyncFieldTimes.RecordUpdateAsync(
                    conn, tx, SyncFieldTimes.AbstinencePeriod, late.Id, late.UpdatedAtMs, repairMs, EndedAtFields, ct).ConfigureAwait(false);
                await ExecAsync(conn, tx,
                    "UPDATE \"abstinence_period\" SET \"ended_at_ms\" = \"started_at_ms\", \"updated_at_ms\" = @ms WHERE \"id\" = @id",
                    ct, ("@ms", repairMs), ("@id", late.Id)).ConfigureAwait(false);
                await ExecAsync(conn, tx,
                    "UPDATE sync_base SET row_json = json_remove(row_json, '$._local_ms') " +
                    "WHERE entity = 'abstinence_period' AND entity_id = @id",
                    ct, ("@id", late.Id)).ConfigureAwait(false);
                repaired++;
            }
        }

        return repaired;
    }

    private static async Task<int> RepairParentCyclesAsync(
        DbConnection conn, DbTransaction? tx, IClock clock, CancellationToken ct)
    {
        var nodes = new Dictionary<string, NodeInfo>(StringComparer.Ordinal);
        await using (var cmd = Command(conn, tx,
            "SELECT h.\"id\", h.\"parent_id\", h.\"updated_at_ms\", COALESCE(f.\"ms\", h.\"updated_at_ms\") " +
            "FROM \"habit_nodes\" AS h " +
            "LEFT JOIN \"sync_field_ms\" AS f " +
            "ON f.\"entity\" = 'habit_node' AND f.\"entity_id\" = h.\"id\" AND f.\"field\" = 'parent_id' " +
            "ORDER BY h.\"id\" ASC"))
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var parentId = await reader.IsDBNullAsync(1, ct).ConfigureAwait(false) ? null : reader.GetString(1);
                var updatedAtMs = reader.GetInt64(2);
                var parentFieldMs = reader.GetInt64(3);
                nodes[id] = new NodeInfo(id, parentId, updatedAtMs, parentFieldMs);
            }
        }

        if (nodes.Count == 0)
            return 0;

        var repaired = 0;
        while (FindCycle(nodes) is { } cycle)
        {
            // Perde lo spostamento più recente misurato sull'ora del campo parent_id (a parità, GUID maggiore).
            var loser = cycle
                .OrderByDescending(n => n.ParentFieldMs)
                .ThenByDescending(n => n.Id, StringComparer.Ordinal)
                .First();

            var previousParent = await ResolvePreviousParentAsync(conn, tx, nodes, loser.Id, loser.ParentId, ct).ConfigureAwait(false);
            var maxCycleMs = cycle.Max(n => n.UpdatedAtMs);
            var repairMs = Math.Max(clock.UtcNowMs, maxCycleMs + 1);

            // Solo parent_id prende l'ora della riparazione: gli altri campi (es. una rinomina concorrente) restano alla loro ora.
            await SyncFieldTimes.RecordUpdateAsync(
                conn, tx, SyncFieldTimes.HabitNode, loser.Id, loser.UpdatedAtMs, repairMs, ParentIdFields, ct).ConfigureAwait(false);
            await ExecAsync(conn, tx,
                "UPDATE \"habit_nodes\" SET \"parent_id\" = @parent, \"updated_at_ms\" = @ms WHERE \"id\" = @id",
                ct, ("@parent", previousParent), ("@ms", repairMs), ("@id", loser.Id)).ConfigureAwait(false);
            await ExecAsync(conn, tx,
                "UPDATE sync_base SET row_json = json_remove(row_json, '$._local_ms') " +
                "WHERE entity = 'habit_node' AND entity_id = @id",
                ct, ("@id", loser.Id)).ConfigureAwait(false);

            nodes[loser.Id] = loser with { ParentId = previousParent, UpdatedAtMs = repairMs, ParentFieldMs = repairMs };
            repaired++;
        }

        return repaired;
    }

    private static List<NodeInfo>? FindCycle(Dictionary<string, NodeInfo> nodes)
    {
        // 0 = non visitato, 1 = nel cammino corrente, 2 = già verificato senza cicli
        var state = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
        foreach (var startId in nodes.Keys.Order(StringComparer.Ordinal))
        {
            if (state.ContainsKey(startId))
                continue;

            var path = new List<string>();
            var currentId = (string?)startId;
            while (currentId is not null && nodes.TryGetValue(currentId, out var current))
            {
                if (state.TryGetValue(currentId, out var s))
                {
                    if (s == 1)
                    {
                        var cycleStart = path.IndexOf(currentId);
                        return path.Skip(cycleStart).Select(id => nodes[id]).ToList();
                    }
                    break;
                }

                state[currentId] = 1;
                path.Add(currentId);
                currentId = current.ParentId;
            }

            foreach (var id in path)
                state[id] = 2;
        }

        return null;
    }

    private static async Task<string?> ResolvePreviousParentAsync(
        DbConnection conn, DbTransaction? tx, Dictionary<string, NodeInfo> nodes, string loserId, string? currentCycleParentId,
        CancellationToken ct)
    {
        await using var cmd = Command(conn, tx,
            "SELECT row_json FROM sync_base WHERE entity = 'habit_node' AND entity_id = @id ORDER BY peer_device_id ASC",
            ("@id", loserId));
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        var candidates = new List<string?>();
        var fallbackCandidates = new List<string?>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            using var doc = JsonDocument.Parse(reader.GetString(0));
            var root = doc.RootElement;
            if (root.TryGetProperty("parent_id", out var parentProp))
            {
                var baseParent = parentProp.ValueKind == JsonValueKind.String ? parentProp.GetString() : null;
                if (!string.Equals(baseParent, currentCycleParentId, StringComparison.Ordinal))
                    candidates.Add(baseParent);
            }

            if (root.TryGetProperty("_prev_parent_id", out var prevProp))
            {
                var prevParent = prevProp.ValueKind == JsonValueKind.String ? prevProp.GetString() : null;
                if (!string.Equals(prevParent, currentCycleParentId, StringComparison.Ordinal))
                    fallbackCandidates.Add(prevParent);
            }
        }

        foreach (var candidate in candidates.Concat(fallbackCandidates))
        {
            if (candidate is null)
                return null;
            if (IsValidNonCyclicParent(nodes, loserId, candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsValidNonCyclicParent(Dictionary<string, NodeInfo> nodes, string nodeId, string candidateParentId)
    {
        if (!nodes.ContainsKey(candidateParentId))
            return false;

        var visited = new HashSet<string>(StringComparer.Ordinal) { nodeId };
        var cursor = (string?)candidateParentId;
        while (cursor is not null)
        {
            if (!visited.Add(cursor))
                return false;
            if (!nodes.TryGetValue(cursor, out var current))
                break;
            cursor = current.ParentId;
        }

        return true;
    }

    private static DbCommand Command(DbConnection conn, DbTransaction? tx, string sql, params (string Name, object? Value)[] pars)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in pars)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }

    private static async Task<int> ExecAsync(
        DbConnection conn, DbTransaction? tx, string sql, CancellationToken ct, params (string, object?)[] pars)
    {
        await using var cmd = Command(conn, tx, sql, pars);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private sealed record OpenPeriod(string Id, long StartedAtMs, long UpdatedAtMs);

    private sealed record NodeInfo(string Id, string? ParentId, long UpdatedAtMs, long ParentFieldMs);
#pragma warning restore CA2100
}
