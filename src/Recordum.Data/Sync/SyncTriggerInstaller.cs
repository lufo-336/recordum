using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Recordum.Data.Sync;

/// <summary>I trigger che rendono la sync indipendente dalla via di scrittura: le ~58 ExecuteDelete/ExecuteUpdate/SQL e
/// le cascade scavalcano <c>TimestampInterceptor</c>, i trigger no. Non stanno in una migrazione: si ricreano dal
/// <see cref="SyncCatalog"/> a ogni avvio, così una tabella nuova li riceve da sola e un DB fatto con
/// <c>EnsureCreated</c> li ha uguali. Chiamano le funzioni che registra <c>SyncConnectionInterceptor</c>.
/// <para>L'UPDATE dentro <c>trg_sync_upd_*</c> non lo riaccende perché <c>recursive_triggers</c> è spento (default).
/// Una modifica batte sempre la versione che modifica: se l'ora nuova non è più alta (stesso millisecondo, o un orologio
/// indietro rispetto a una riga arrivata da un pari) diventa la vecchia + 1.</para></summary>
public static class SyncTriggerInstaller
{
    private const string SettingsTable = "app_settings";

    private static readonly string Script = BuildScript();

    /// <summary>Idempotente: per ogni tabella che viaggia ricrea <c>trg_sync_del_&lt;tabella&gt;</c>,
    /// <c>trg_sync_upd_&lt;tabella&gt;</c>, <c>trg_sync_ins_&lt;tabella&gt;</c> e (dove ci sono campi con ora propria)
    /// <c>trg_sync_fms_&lt;tabella&gt;</c> (per <c>app_settings</c> solo sulle chiavi che viaggiano).</summary>
    public static async Task EnsureAsync(RecordumDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync(Script, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static string BuildScript()
    {
        var keys = string.Join(", ", SyncCatalog.TravellingSettingKeys.Order(StringComparer.Ordinal).Select(Literal));
        var sql = new StringBuilder();
        foreach (var e in SyncCatalog.Entities)
        {
            var table = Quote(e.Table);
            var entityLiteral = Literal(e.PublicName);
            // L'identità della riga: i valori salvati così come sono (i GUID in TEXT maiuscolo), uniti da '|'.
            var oldIdentity = string.Join(" || '|' || ", e.KeyColumns.Select(c => $"OLD.{Quote(c)}"));
            var newIdentity = string.Join(" || '|' || ", e.KeyColumns.Select(c => $"NEW.{Quote(c)}"));
            var match = string.Join(" AND ", e.KeyColumns.Select(c => $"{Quote(c)} = NEW.{Quote(c)}"));
            var oldKeyFilter = e.Table == SettingsTable ? $"OLD.\"key\" IN ({keys}) AND " : "";
            var newKeyFilter = e.Table == SettingsTable ? $"NEW.\"key\" IN ({keys}) AND " : "";

            sql.Append(CultureInfo.InvariantCulture, $"""
                DROP TRIGGER IF EXISTS "trg_sync_del_{e.Table}";
                CREATE TRIGGER "trg_sync_del_{e.Table}" AFTER DELETE ON {table}
                WHEN {oldKeyFilter}recordum_sync_applying() = 0
                BEGIN
                    INSERT OR REPLACE INTO sync_tombstone (entity, entity_id, deleted_at_ms, device_id, alias_of)
                    VALUES ({entityLiteral}, {oldIdentity}, recordum_now_ms(), NULL, NULL);
                    DELETE FROM sync_field_ms WHERE entity = {entityLiteral} AND entity_id = {oldIdentity};
                END;
                DROP TRIGGER IF EXISTS "trg_sync_upd_{e.Table}";
                CREATE TRIGGER "trg_sync_upd_{e.Table}" AFTER UPDATE ON {table}
                WHEN {newKeyFilter}NEW.updated_at_ms <= OLD.updated_at_ms AND recordum_sync_applying() = 0
                BEGIN
                    UPDATE {table} SET updated_at_ms = MAX(recordum_now_ms(), OLD.updated_at_ms + 1) WHERE {match};
                END;
                DROP TRIGGER IF EXISTS "trg_sync_ins_{e.Table}";
                CREATE TRIGGER "trg_sync_ins_{e.Table}" AFTER INSERT ON {table}
                WHEN {newKeyFilter}recordum_sync_applying() = 0
                BEGIN
                    -- Se la stessa identità era stata cancellata nello stesso millisecondo (o dopo), la riga nuova vince
                    -- sulla lapide e la rimuove; il filtro evita aggiornamenti inutili che riaccenderebbero upd/fms.
                    UPDATE {table}
                    SET updated_at_ms = MAX(
                        NEW.updated_at_ms,
                        (SELECT deleted_at_ms + 1 FROM sync_tombstone
                         WHERE entity = {entityLiteral} AND entity_id = {newIdentity} AND alias_of IS NULL))
                    WHERE {match}
                      AND EXISTS (
                        SELECT 1 FROM sync_tombstone
                        WHERE entity = {entityLiteral} AND entity_id = {newIdentity} AND alias_of IS NULL AND deleted_at_ms >= NEW.updated_at_ms);
                    DELETE FROM sync_tombstone WHERE entity = {entityLiteral} AND entity_id = {newIdentity} AND alias_of IS NULL;
                    DELETE FROM sync_field_ms WHERE entity = {entityLiteral} AND entity_id = {newIdentity};
                END;
                DROP TRIGGER IF EXISTS "trg_sync_fms_{e.Table}";

                """);

            var timedFields = SyncCatalog.TimedFields(e);
            if (timedFields.Count > 0)
            {
                // Un UPDATE che tocca solo updated_at_ms (quello di trg_sync_upd, che non si spegne con recursive_triggers
                // perché è un altro trigger) non cambia nessun campo: se scrivesse, rimetterebbe all'ora vecchia i campi
                // appena cambiati. ponytail: anche un UPDATE a mano della sola ora si salta; i campi prendono la nuova.
                var anyChanged = string.Join(" OR ", timedFields.Select(f => $"OLD.{Quote(f.Column)} IS NOT NEW.{Quote(f.Column)}"));
                sql.Append(CultureInfo.InvariantCulture, $"""
                    CREATE TRIGGER "trg_sync_fms_{e.Table}" AFTER UPDATE ON {table}
                    WHEN {newKeyFilter}recordum_sync_applying() = 0 AND (NEW.updated_at_ms IS OLD.updated_at_ms OR {anyChanged})
                    BEGIN

                    """);
                foreach (var f in timedFields)
                {
                    var col = Quote(f.Column);
                    var fieldLiteral = Literal(f.PublicName);
                    sql.Append(CultureInfo.InvariantCulture, $"""
                            INSERT OR IGNORE INTO sync_field_ms (entity, entity_id, field, ms)
                            SELECT {entityLiteral}, {newIdentity}, {fieldLiteral}, OLD.updated_at_ms
                            WHERE OLD.{col} IS NEW.{col};
                            DELETE FROM sync_field_ms
                            WHERE entity = {entityLiteral} AND entity_id = {oldIdentity} AND field = {fieldLiteral}
                              AND OLD.{col} IS NOT NEW.{col};

                        """);
                }
                sql.Append("END;\n");
            }
        }
        return sql.ToString();
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
