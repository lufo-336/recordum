using System.Data.Common;

namespace Recordum.Data.Sync;

/// <summary>
/// Mantiene l'invariante di <c>sync_field_ms</c> durante le scritture dell'unione (quando i trigger sono spenti),
/// con la stessa semantica di <c>trg_sync_fms_*</c> in <see cref="SyncTriggerInstaller"/>: i campi con ora propria
/// non cambiati conservano l'ora vecchia della riga se questa sale, mentre quelli cambiati prendono la nuova ora.
/// </summary>
internal static class SyncFieldTimes
{
    private static readonly SyncEntity AbstinencePeriodEntity = SyncCatalog.Get("abstinence_period");
    private static readonly SyncEntity HabitNodeEntity = SyncCatalog.Get("habit_node");

    internal static SyncEntity AbstinencePeriod => AbstinencePeriodEntity;

    internal static SyncEntity HabitNode => HabitNodeEntity;

#pragma warning disable CA2100 // SQL parametrico su identificatori costanti; solo valori come parametri
    /// <summary>
    /// Prima di un UPDATE che cambia i campi in <paramref name="changedPublicFields"/> e porta l'ora della riga da
    /// <paramref name="oldRowMs"/> a <paramref name="newRowMs"/>: registra <paramref name="oldRowMs"/> sui campi
    /// non cambiati (se la riga sale e non hanno già un'ora più vecchia) e rimuove eventuali righe dai campi cambiati.
    /// </summary>
    internal static async Task RecordUpdateAsync(
        DbConnection conn,
        DbTransaction? tx,
        SyncEntity entity,
        string entityId,
        long oldRowMs,
        long newRowMs,
        IReadOnlySet<string> changedPublicFields,
        CancellationToken ct)
    {
        foreach (var field in SyncCatalog.TimedFields(entity))
        {
            if (changedPublicFields.Contains(field.PublicName))
            {
                await ExecAsync(conn, tx,
                    "DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @id AND field = @f",
                    ct, ("@e", entity.PublicName), ("@id", entityId), ("@f", field.PublicName)).ConfigureAwait(false);
            }
            else if (oldRowMs < newRowMs)
            {
                await ExecAsync(conn, tx,
                    "INSERT OR IGNORE INTO sync_field_ms (entity, entity_id, field, ms) VALUES (@e, @id, @f, @ms)",
                    ct, ("@e", entity.PublicName), ("@id", entityId), ("@f", field.PublicName), ("@ms", oldRowMs)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Copia le ore dei campi dalla riga <paramref name="sourceId"/> alla riga <paramref name="targetId"/>,
    /// sostituendo eventuali ore già presenti su <paramref name="targetId"/>.
    /// </summary>
    internal static async Task CopyAsync(
        DbConnection conn,
        DbTransaction? tx,
        string entityPublicName,
        string sourceId,
        string targetId,
        CancellationToken ct)
    {
        await ExecAsync(conn, tx,
            "DELETE FROM sync_field_ms WHERE entity = @e AND entity_id = @target",
            ct, ("@e", entityPublicName), ("@target", targetId)).ConfigureAwait(false);
        await ExecAsync(conn, tx,
            "INSERT INTO sync_field_ms (entity, entity_id, field, ms) " +
            "SELECT entity, @target, field, ms FROM sync_field_ms WHERE entity = @e AND entity_id = @source",
            ct, ("@target", targetId), ("@e", entityPublicName), ("@source", sourceId)).ConfigureAwait(false);
    }

    private static async Task ExecAsync(
        DbConnection conn, DbTransaction? tx, string sql, CancellationToken ct, params (string Name, object? Value)[] pars)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in pars)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
#pragma warning restore CA2100
}
