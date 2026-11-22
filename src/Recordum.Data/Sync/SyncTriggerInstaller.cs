using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Recordum.Data.Sync;

/// <summary>I trigger che rendono la sync indipendente dalla via di scrittura: le ~58 ExecuteDelete/ExecuteUpdate/SQL e
/// le cascade scavalcano <c>TimestampInterceptor</c>, i trigger no. Non stanno in una migrazione: si ricreano dal
/// <see cref="SyncCatalog"/> a ogni avvio, così una tabella nuova li riceve da sola e un DB fatto con
/// <c>EnsureCreated</c> li ha uguali. Chiamano le funzioni che registra <c>SyncConnectionInterceptor</c>.
/// <para>L'UPDATE dentro <c>trg_sync_upd_*</c> non lo riaccende perché <c>recursive_triggers</c> è spento (default).</para></summary>
public static class SyncTriggerInstaller
{
    private const string SettingsTable = "app_settings";

    private static readonly string Script = BuildScript();

    /// <summary>Idempotente: per ogni tabella che viaggia ricrea <c>trg_sync_del_&lt;tabella&gt;</c> e
    /// <c>trg_sync_upd_&lt;tabella&gt;</c> (per <c>app_settings</c> solo sulle chiavi che viaggiano).</summary>
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
            // L'identità della riga: i valori salvati così come sono (i GUID in TEXT maiuscolo), uniti da '|'.
            var identity = string.Join(" || '|' || ", e.KeyColumns.Select(c => $"OLD.{Quote(c)}"));
            var match = string.Join(" AND ", e.KeyColumns.Select(c => $"{Quote(c)} = NEW.{Quote(c)}"));
            var oldKeyFilter = e.Table == SettingsTable ? $"OLD.\"key\" IN ({keys}) AND " : "";
            var newKeyFilter = e.Table == SettingsTable ? $"NEW.\"key\" IN ({keys}) AND " : "";

            sql.Append(CultureInfo.InvariantCulture, $"""
                DROP TRIGGER IF EXISTS "trg_sync_del_{e.Table}";
                CREATE TRIGGER "trg_sync_del_{e.Table}" AFTER DELETE ON {table}
                WHEN {oldKeyFilter}recordum_sync_applying() = 0
                BEGIN
                    INSERT OR REPLACE INTO sync_tombstone (entity, entity_id, deleted_at_ms, device_id, alias_of)
                    VALUES ({Literal(e.PublicName)}, {identity}, recordum_now_ms(), NULL, NULL);
                END;
                DROP TRIGGER IF EXISTS "trg_sync_upd_{e.Table}";
                CREATE TRIGGER "trg_sync_upd_{e.Table}" AFTER UPDATE ON {table}
                WHEN {newKeyFilter}NEW.updated_at_ms IS OLD.updated_at_ms AND recordum_sync_applying() = 0
                BEGIN
                    UPDATE {table} SET updated_at_ms = recordum_now_ms() WHERE {match};
                END;

                """);
        }
        return sql.ToString();
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
