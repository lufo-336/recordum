using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Recordum.Data.Sync;

/// <summary>
/// Unifica le righe che rappresentano la stessa entità per chiave naturale (spec §8.3, <c>docs/formats/sync-file.md</c> §6.5):
/// resta il GUID minore (ordinale), tutte le FK del modello EF che puntano alla tabella vengono riscritte sul GUID
/// tenuto, e il GUID scartato diventa una lapide in <c>sync_tombstone</c> con <c>alias_of</c> valorizzato.
/// </summary>
public static partial class NaturalKeyUnifier
{
    private const string IdCol = "id";
    private const string MsCol = "updated_at_ms";

    // Unione commutativa, associativa e idempotente delle lapidi: vince l'ora più alta per deleted_at_ms e
    // device_id (a parità il minore non NULL), mentre alias_of si preserva sempre (a parità di presenza il minore).
    internal const string UpsertTombstoneSql =
        "INSERT INTO sync_tombstone (entity, entity_id, deleted_at_ms, device_id, alias_of) VALUES (@e, @id, @at, @dev, @alias) " +
        "ON CONFLICT (entity, entity_id) DO UPDATE SET " +
        "device_id = CASE " +
        "WHEN excluded.deleted_at_ms > sync_tombstone.deleted_at_ms THEN excluded.device_id " +
        "WHEN sync_tombstone.deleted_at_ms > excluded.deleted_at_ms THEN sync_tombstone.device_id " +
        "WHEN sync_tombstone.device_id IS NULL THEN excluded.device_id " +
        "WHEN excluded.device_id IS NULL THEN sync_tombstone.device_id " +
        "ELSE MIN(sync_tombstone.device_id, excluded.device_id) END, " +
        "deleted_at_ms = MAX(sync_tombstone.deleted_at_ms, excluded.deleted_at_ms), " +
        "alias_of = CASE " +
        "WHEN sync_tombstone.alias_of IS NULL THEN excluded.alias_of " +
        "WHEN excluded.alias_of IS NULL THEN sync_tombstone.alias_of " +
        "ELSE MIN(sync_tombstone.alias_of, excluded.alias_of) END";

#pragma warning disable CA2100 // SQL costruito solo da identificatori del catalogo e del modello EF; i valori sono parametri
    internal static async Task RenameAsync(
        DbConnection conn, DbTransaction? tx, IModel model, SyncEntity entity, string fromId, string toId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.KeyColumns is not [IdCol] || string.Equals(fromId, toId, StringComparison.Ordinal))
            return;

        foreach (var fk in IncomingForeignKeys(model, entity.Table))
        {
            var depEntity = SyncCatalog.Entities.FirstOrDefault(e => e.Table == fk.DepTable);
            var dropped = depEntity is not null && depEntity.NaturalKey.Contains(fk.DepCol, StringComparer.Ordinal)
                ? await DropUniqueIndexesAsync(conn, tx, fk.DepTable, ct).ConfigureAwait(false)
                : [];
            try
            {
                await RewriteForeignKeyAsync(conn, tx, fk, fromId, toId, ct).ConfigureAwait(false);
                if (dropped.Count > 0 && depEntity is not null)
                    await UnifyAsync(conn, tx, model, depEntity, ct).ConfigureAwait(false);
            }
            finally
            {
                foreach (var sql in dropped)
                    await ExecAsync(conn, tx, sql, ct).ConfigureAwait(false);
            }
        }

        await ExecAsync(conn, tx,
            $"UPDATE {Q(entity.Table)} SET {Q(IdCol)} = @to WHERE {Q(IdCol)} = @from",
            ct, ("@to", toId), ("@from", fromId)).ConfigureAwait(false);

        await ExecAsync(conn, tx,
            "DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @to",
            ct, ("@e", entity.PublicName), ("@to", toId)).ConfigureAwait(false);
        await ExecAsync(conn, tx,
            "UPDATE sync_field_ms SET entity_id = @to WHERE entity = @e AND entity_id = @from",
            ct, ("@to", toId), ("@e", entity.PublicName), ("@from", fromId)).ConfigureAwait(false);

        await ExecAsync(conn, tx,
            "INSERT OR IGNORE INTO sync_base (peer_device_id, entity, entity_id, row_json) " +
            "SELECT peer_device_id, entity, @to, json_remove(row_json, '$._local_ms') " +
            "FROM sync_base WHERE entity = @e AND entity_id = @from",
            ct, ("@to", toId), ("@e", entity.PublicName), ("@from", fromId)).ConfigureAwait(false);
        await ExecAsync(conn, tx,
            "DELETE FROM sync_base WHERE entity = @e AND entity_id = @from",
            ct, ("@e", entity.PublicName), ("@from", fromId)).ConfigureAwait(false);
    }

    internal static async Task<int> UnifyAsync(
        DbConnection conn, DbTransaction? tx, IModel model, SyncEntity entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.KeyColumns is not [IdCol])
            return 0;

        var rows = await ReadRowsAsync(conn, tx, entity, ct).ConfigureAwait(false);
        if (rows.Count <= 1)
            return 0;

        var byId = rows.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var groups = new List<List<DbRow>>();

        if (entity.NaturalKey.Count > 0)
        {
            var nocase = await NocaseColumnsAsync(conn, tx, entity.Table, ct).ConfigureAwait(false);
            var byNaturalKey = new Dictionary<string, List<DbRow>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = BuildNaturalKey(row, entity.NaturalKey, nocase);
                if (!byNaturalKey.TryGetValue(key, out var list))
                {
                    list = [];
                    byNaturalKey[key] = list;
                }
                list.Add(row);
            }

            foreach (var group in byNaturalKey.Values.Where(g => g.Count > 1))
                groups.Add(group);
        }

        // Righe locali ancora presenti il cui ID ha già ricevuto una lapide con alias_of verso un'altra riga presente.
        var existingAliases = await ReadAliasesAsync(conn, tx, entity.PublicName, ct).ConfigureAwait(false);
        if (existingAliases.Count > 0)
        {
            var groupedIds = new HashSet<string>(groups.SelectMany(g => g.Select(r => r.Id)), StringComparer.Ordinal);
            foreach (var (loserId, rawTarget) in existingAliases)
            {
                if (!byId.TryGetValue(loserId, out var loserRow) || groupedIds.Contains(loserId))
                    continue;
                var targetId = ResolveAlias(existingAliases, rawTarget);
                if (targetId != loserId && byId.TryGetValue(targetId, out var targetRow))
                {
                    var existingGroup = groups.FirstOrDefault(g => g.Any(r => r.Id == targetId));
                    if (existingGroup is not null)
                    {
                        existingGroup.Add(loserRow);
                    }
                    else
                    {
                        groups.Add([targetRow, loserRow]);
                    }
                    groupedIds.Add(loserId);
                    groupedIds.Add(targetId);
                }
            }
        }

        if (groups.Count == 0)
            return 0;

        var codec = new SyncRowCodec(model);
        var fks = IncomingForeignKeys(model, entity.Table);
        var repaired = 0;

        var timedFields = SyncCatalog.TimedFields(entity).Where(f => f.Kind != SyncFieldKind.Photo).ToList();
        var hasPhotoFields = timedFields.Count != SyncCatalog.TimedFields(entity).Count;

        foreach (var group in groups)
        {
            group.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            var winner = group[0];
            var fieldMsById = await ReadFieldMsAsync(conn, tx, entity.PublicName, group, ct).ConfigureAwait(false);
            var publicById = group.ToDictionary(
                r => r.Id,
                r => codec.ToPublic(entity, r.Raw).Fields,
                StringComparer.Ordinal);
            var maxGroupMs = group.Max(r => r.UpdatedAtMs);

            // Unificazione campo per campo (Ruling 20): vince il valore con l'ora di campo maggiore, a parità la forma
            // canonica minore in UTF-8. Tutto si scrive sul vincitore prima di togliere i perdenti.
            var winningRaw = new Dictionary<string, object?>(timedFields.Count, StringComparer.Ordinal);
            var winningFieldMs = new Dictionary<string, long>(timedFields.Count, StringComparer.Ordinal);
            foreach (var field in timedFields)
            {
                var bestRow = group[0];
                var bestVal = publicById[bestRow.Id][field.PublicName];
                var bestMs = GetFieldMs(fieldMsById, bestRow, field.PublicName);
                for (var i = 1; i < group.Count; i++)
                {
                    var candRow = group[i];
                    var candVal = publicById[candRow.Id][field.PublicName];
                    var candMs = GetFieldMs(fieldMsById, candRow, field.PublicName);
                    if (candMs > bestMs || (candMs == bestMs && SyncMerger.CompareCanonicalValue(candVal, bestVal) < 0))
                    {
                        bestRow = candRow;
                        bestVal = candVal;
                        bestMs = candMs;
                    }
                }
                winningRaw[field.Column] = bestRow.Raw[field.Column];
                winningFieldMs[field.PublicName] = bestMs;
            }

            var updateCols = timedFields.Select(f => f.Column).Append(MsCol).ToArray();
            var setClause = string.Join(", ", updateCols.Select((c, i) => $"{Q(c)} = @v{i}"));
            var pars = new (string, object?)[updateCols.Length + 1];
            for (var i = 0; i < timedFields.Count; i++)
                pars[i] = ($"@v{i}", winningRaw[timedFields[i].Column]);
            pars[timedFields.Count] = ($"@v{timedFields.Count}", maxGroupMs);
            pars[updateCols.Length] = ("@id", winner.Id);
            await ExecAsync(conn, tx,
                $"UPDATE {Q(entity.Table)} SET {setClause} WHERE {Q(IdCol)} = @id",
                ct, pars).ConfigureAwait(false);

            if (!hasPhotoFields)
            {
                await ExecAsync(conn, tx,
                    "DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @id",
                    ct, ("@e", entity.PublicName), ("@id", winner.Id)).ConfigureAwait(false);
            }
            else
            {
                foreach (var field in timedFields)
                {
                    await ExecAsync(conn, tx,
                        "DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @id AND field = @f",
                        ct, ("@e", entity.PublicName), ("@id", winner.Id), ("@f", field.PublicName)).ConfigureAwait(false);
                }
            }

            foreach (var (fieldName, winMs) in winningFieldMs)
            {
                if (winMs < maxGroupMs)
                {
                    await ExecAsync(conn, tx,
                        "INSERT INTO sync_field_ms (entity, entity_id, field, ms) VALUES (@e, @id, @f, @ms)",
                        ct, ("@e", entity.PublicName), ("@id", winner.Id), ("@f", fieldName), ("@ms", winMs)).ConfigureAwait(false);
                }
            }

            foreach (var loser in group.Skip(1))
            {
                foreach (var fk in fks)
                {
                    var depEntity = SyncCatalog.Entities.FirstOrDefault(e => e.Table == fk.DepTable);
                    var dropped = depEntity is not null && depEntity.NaturalKey.Contains(fk.DepCol, StringComparer.Ordinal)
                        ? await DropUniqueIndexesAsync(conn, tx, fk.DepTable, ct).ConfigureAwait(false)
                        : [];
                    try
                    {
                        await RewriteForeignKeyAsync(conn, tx, fk, loser.Id, winner.Id, ct).ConfigureAwait(false);
                        if (dropped.Count > 0 && depEntity is not null)
                            repaired += await UnifyAsync(conn, tx, model, depEntity, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        foreach (var sql in dropped)
                            await ExecAsync(conn, tx, sql, ct).ConfigureAwait(false);
                    }
                }

                await ExecAsync(conn, tx,
                    "INSERT OR IGNORE INTO sync_base (peer_device_id, entity, entity_id, row_json) " +
                    "SELECT peer_device_id, entity, @winner, json_remove(row_json, '$._local_ms') " +
                    "FROM sync_base WHERE entity = @e AND entity_id = @loser",
                    ct, ("@winner", winner.Id), ("@e", entity.PublicName), ("@loser", loser.Id)).ConfigureAwait(false);
                await ExecAsync(conn, tx,
                    "DELETE FROM sync_base WHERE entity = @e AND entity_id = @loser",
                    ct, ("@e", entity.PublicName), ("@loser", loser.Id)).ConfigureAwait(false);

                await ExecAsync(conn, tx,
                    $"DELETE FROM {Q(entity.Table)} WHERE {Q(IdCol)} = @loser",
                    ct, ("@loser", loser.Id)).ConfigureAwait(false);
                await ExecAsync(conn, tx,
                    "DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @loser",
                    ct, ("@e", entity.PublicName), ("@loser", loser.Id)).ConfigureAwait(false);

                await ExecAsync(conn, tx,
                    UpsertTombstoneSql,
                    ct, ("@e", entity.PublicName), ("@id", loser.Id), ("@at", maxGroupMs), ("@dev", null), ("@alias", winner.Id)).ConfigureAwait(false);

                await ExecAsync(conn, tx,
                    "UPDATE sync_tombstone SET alias_of = @winner WHERE entity = @e AND alias_of = @loser",
                    ct, ("@winner", winner.Id), ("@e", entity.PublicName), ("@loser", loser.Id)).ConfigureAwait(false);

                repaired++;
            }

            await ExecAsync(conn, tx,
                "UPDATE sync_base SET row_json = json_remove(row_json, '$._local_ms') " +
                "WHERE entity = @e AND entity_id = @winner",
                ct, ("@e", entity.PublicName), ("@winner", winner.Id)).ConfigureAwait(false);
        }

        return repaired;
    }

    internal static async Task<List<string>> DropUniqueIndexesAsync(
        DbConnection conn, DbTransaction? tx, string table, CancellationToken ct)
    {
        await using var cmd = Command(conn, tx,
            "SELECT name, sql FROM sqlite_master WHERE type = 'index' AND tbl_name = @t AND sql LIKE 'CREATE UNIQUE INDEX%'",
            ("@t", table));
        var indexes = new List<(string Name, string Sql)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                indexes.Add((reader.GetString(0), reader.GetString(1)));
        }

        var createSqls = new List<string>(indexes.Count);
        foreach (var (name, sql) in indexes)
        {
            await ExecAsync(conn, tx, $"DROP INDEX IF EXISTS {Q(name)}", ct).ConfigureAwait(false);
            createSqls.Add(sql);
        }

        return createSqls;
    }

    private static async Task RewriteForeignKeyAsync(
        DbConnection conn, DbTransaction? tx, IncomingFk fk, string loserId, string winnerId, CancellationToken ct)
    {
        if (fk.DepPkCols.Contains(fk.DepCol, StringComparer.Ordinal))
        {
            var otherPk = fk.DepPkCols.Where(c => !string.Equals(c, fk.DepCol, StringComparison.Ordinal)).ToArray();
            var matchOther = string.Join(" AND ", otherPk.Select(c => $"l.{Q(c)} = w.{Q(c)}"));

            if (fk.HasUpdatedAtMs)
            {
                await ExecAsync(conn, tx,
                    $"UPDATE {Q(fk.DepTable)} AS w SET {Q(MsCol)} = MAX(w.{Q(MsCol)}, (" +
                    $"SELECT l.{Q(MsCol)} FROM {Q(fk.DepTable)} AS l WHERE l.{Q(fk.DepCol)} = @loser AND {matchOther})) " +
                    $"WHERE w.{Q(fk.DepCol)} = @winner AND EXISTS (" +
                    $"SELECT 1 FROM {Q(fk.DepTable)} AS l WHERE l.{Q(fk.DepCol)} = @loser AND {matchOther})",
                    ct, ("@loser", loserId), ("@winner", winnerId)).ConfigureAwait(false);
            }

            var matchDelete = string.Join(" AND ", otherPk.Select(c => $"w.{Q(c)} = l.{Q(c)}"));
            await ExecAsync(conn, tx,
                $"DELETE FROM {Q(fk.DepTable)} AS l WHERE l.{Q(fk.DepCol)} = @loser AND EXISTS (" +
                $"SELECT 1 FROM {Q(fk.DepTable)} AS w WHERE w.{Q(fk.DepCol)} = @winner AND {matchDelete})",
                ct, ("@loser", loserId), ("@winner", winnerId)).ConfigureAwait(false);
        }

        // Le riscritture delle FK non cambiano le ore dei campi: ogni dispositivo fa la stessa riscrittura, quindi il
        // valore è uguale ovunque.
        // ponytail: quando la FK fa parte della chiave (habit_node_tag, note_tag) l'identità cambia e le ore dei campi
        // vecchie restano orfane (la pulizia di fine unione le toglie, quindi i campi prendono l'ora della riga).
        await ExecAsync(conn, tx,
            $"UPDATE {Q(fk.DepTable)} SET {Q(fk.DepCol)} = @winner WHERE {Q(fk.DepCol)} = @loser",
            ct, ("@winner", winnerId), ("@loser", loserId)).ConfigureAwait(false);
    }

    private static long GetFieldMs(
        Dictionary<string, Dictionary<string, long>> fieldMsById, DbRow row, string fieldPublicName) =>
        fieldMsById[row.Id].TryGetValue(fieldPublicName, out var ms) ? ms : row.UpdatedAtMs;

    private static async Task<Dictionary<string, Dictionary<string, long>>> ReadFieldMsAsync(
        DbConnection conn, DbTransaction? tx, string entityName, List<DbRow> group, CancellationToken ct)
    {
        var map = new Dictionary<string, Dictionary<string, long>>(group.Count, StringComparer.Ordinal);
        foreach (var row in group)
            map[row.Id] = new Dictionary<string, long>(StringComparer.Ordinal);

        var pars = new (string, object?)[group.Count + 1];
        pars[0] = ("@e", entityName);
        var inList = new string[group.Count];
        for (var i = 0; i < group.Count; i++)
        {
            var p = $"@id{i}";
            inList[i] = p;
            pars[i + 1] = (p, group[i].Id);
        }

        await using var cmd = Command(conn, tx,
            $"SELECT entity_id, field, ms FROM sync_field_ms WHERE entity = @e AND entity_id IN ({string.Join(", ", inList)})",
            pars);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            map[reader.GetString(0)][reader.GetString(1)] = reader.GetInt64(2);
        return map;
    }

    private static string BuildNaturalKey(DbRow row, IReadOnlyList<string> naturalKeyCols, HashSet<string> nocase)
    {
        if (naturalKeyCols.Count == 1)
            return FormatKeyPart(row.Raw[naturalKeyCols[0]], nocase.Contains(naturalKeyCols[0]));

        var parts = new string[naturalKeyCols.Count];
        for (var i = 0; i < naturalKeyCols.Count; i++)
            parts[i] = FormatKeyPart(row.Raw[naturalKeyCols[i]], nocase.Contains(naturalKeyCols[i]));
        return string.Join("\0", parts);
    }

    private static string FormatKeyPart(object? value, bool nocase)
    {
        var s = value switch
        {
            null or DBNull => "",
            string str => str,
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        };
        return nocase ? s.ToUpperInvariant() : s;
    }

    // Dallo schema SQLite: nel modello di runtime di EF la collazione non c'è (read-optimized). Le colonne sono scritte
    // da EF come "nome" TEXT ... COLLATE NOCASE.
    private static async Task<HashSet<string>> NocaseColumnsAsync(DbConnection conn, DbTransaction? tx, string table, CancellationToken ct)
    {
        await using var cmd = Command(conn, tx, "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @t", ("@t", table));
        var sql = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string ?? "";
        return NocaseColumnRegex().Matches(sql).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    [System.Text.RegularExpressions.GeneratedRegex("\"(\\w+)\"[^,\"]*COLLATE NOCASE", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex NocaseColumnRegex();

    private static List<IncomingFk> IncomingForeignKeys(IModel model, string principalTable)
    {
        var list = new List<IncomingFk>();
        foreach (var et in model.GetEntityTypes())
        {
            var depTable = et.GetTableName();
            var pk = et.FindPrimaryKey();
            if (depTable is null || pk is null)
                continue;
            var depStore = StoreObjectIdentifier.Table(depTable, et.GetSchema());
            var pkCols = pk.Properties.Select(p => p.GetColumnName(depStore)!).ToArray();
            var hasMs = et.GetProperties().Any(p => string.Equals(p.GetColumnName(depStore), MsCol, StringComparison.Ordinal));

            foreach (var fk in et.GetForeignKeys())
            {
                var pTable = fk.PrincipalEntityType.GetTableName();
                if (!string.Equals(pTable, principalTable, StringComparison.Ordinal) || fk.Properties.Count != 1)
                    continue;
                var depCol = fk.Properties[0].GetColumnName(depStore)!;
                list.Add(new IncomingFk(depTable, depCol, pkCols, hasMs));
            }
        }
        return list;
    }

    private static async Task<List<DbRow>> ReadRowsAsync(
        DbConnection conn, DbTransaction? tx, SyncEntity entity, CancellationToken ct)
    {
        var cols = entity.Fields.Where(f => f.Kind != SyncFieldKind.Photo).Select(f => f.Column).ToArray();
        var sql = $"SELECT {string.Join(", ", cols.Select(Q))} FROM {Q(entity.Table)} ORDER BY {Q(IdCol)}";
        await using var cmd = Command(conn, tx, sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<DbRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var raw = new Dictionary<string, object?>(cols.Length, StringComparer.Ordinal);
            for (var i = 0; i < cols.Length; i++)
                raw[cols[i]] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false) ? null : reader.GetValue(i);
            var id = (string)raw[IdCol]!;
            var ms = Convert.ToInt64(raw[MsCol], System.Globalization.CultureInfo.InvariantCulture);
            list.Add(new DbRow(id, ms, raw));
        }
        return list;
    }

    private static async Task<Dictionary<string, string>> ReadAliasesAsync(
        DbConnection conn, DbTransaction? tx, string entityName, CancellationToken ct)
    {
        await using var cmd = Command(conn, tx,
            "SELECT entity_id, alias_of FROM sync_tombstone WHERE entity = @e AND alias_of IS NOT NULL",
            ("@e", entityName));
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            map[reader.GetString(0)] = reader.GetString(1);
        return map;
    }

    private static string ResolveAlias(Dictionary<string, string> aliases, string id)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { id };
        while (aliases.TryGetValue(id, out var next) && visited.Add(next))
            id = next;
        return id;
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

    private static string Q(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record DbRow(string Id, long UpdatedAtMs, Dictionary<string, object?> Raw);

    private sealed record IncomingFk(string DepTable, string DepCol, string[] DepPkCols, bool HasUpdatedAtMs);
#pragma warning restore CA2100
}
