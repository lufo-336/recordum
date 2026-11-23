using System.Data;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Recordum.Data.Sync;

public sealed record SyncWriteOptions(
    string DeviceId,
    string DeviceName,
    string AppVersion,
    bool IncludePhotos,
    long WrittenAtMs,
    IReadOnlyDictionary<string, long>? MergedFrom = null);

/// <summary>
/// Scrive lo stato completo del database in un archivio ZIP <c>recordum-sync</c> versione <c>1.0</c>.
/// Legge i valori salvati in SQLite e riversa le foto una alla volta con <see cref="SqliteDataReader.GetStream"/>.
/// </summary>
public static class SyncArchiveWriter
{
    public const string FormatName = "recordum-sync";
    public const string CurrentFormatVersion = "1.0";

    private static readonly byte[] NewLine = [(byte)'\n'];

    // Il file non e' mai HTML: niente \u00e8 per le lettere accentate, i diff restano leggibili.
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static async Task WriteAsync(
        RecordumDbContext db,
        Stream zipOut,
        SyncWriteOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(zipOut);
        ArgumentNullException.ThrowIfNull(options);

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct).ConfigureAwait(false);

        // Una sola transazione di lettura: righe, lapidi e foto vengono dallo stesso istante.
        var ownTx = db.Database.CurrentTransaction is null;
        var efTx = db.Database.CurrentTransaction ?? await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var tx = (SqliteTransaction)efTx.GetDbTransaction();
        try
        {
            await WriteCoreAsync(db, conn, tx, zipOut, options, ct).ConfigureAwait(false);
        }
        finally
        {
            if (ownTx)
                await efTx.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task WriteCoreAsync(
        RecordumDbContext db,
        SqliteConnection conn,
        SqliteTransaction tx,
        Stream zipOut,
        SyncWriteOptions options,
        CancellationToken ct)
    {
        var codec = new SyncRowCodec(db.Model);
        var entityCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        using var archive = new ZipArchive(zipOut, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var entity in SyncCatalog.Entities)
        {
            var rows = await ReadEntityRowsAsync(conn, tx, codec, entity, ct).ConfigureAwait(false);
            entityCounts[entity.PublicName] = rows.Count;

            var entry = archive.CreateEntry($"data/{entity.PublicName}.jsonl", CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await WriteEntityJsonLinesAsync(entryStream, rows, ct).ConfigureAwait(false);
        }

        var tombstones = await ReadTombstonesAsync(conn, tx, ct).ConfigureAwait(false);
        {
            var tombstoneEntry = archive.CreateEntry("tombstones.jsonl", CompressionLevel.Optimal);
            await using var tombstoneStream = tombstoneEntry.Open();
            await WriteTombstonesJsonLinesAsync(tombstoneStream, tombstones, ct).ConfigureAwait(false);
        }

        await WritePhotosAsync(conn, tx, archive, options.IncludePhotos, ct).ConfigureAwait(false);

        var manifest = new SyncManifest(
            Format: FormatName,
            FormatVersion: CurrentFormatVersion,
            DeviceId: options.DeviceId,
            DeviceName: options.DeviceName,
            AppVersion: options.AppVersion,
            WrittenAtMs: options.WrittenAtMs,
            IncludesPhotos: options.IncludePhotos,
            Entities: entityCounts)
        {
            // TODO(Task 9 wiring): SyncService passa qui, per ogni pari, il written_at_ms dell'ultimo suo file unito.
            MergedFrom = options.MergedFrom ?? new Dictionary<string, long>(),
        };

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var manifestStream = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, ct).ConfigureAwait(false);
        }
    }

    internal static async Task<List<SyncRow>> ReadEntityRowsAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        SyncRowCodec codec,
        SyncEntity entity,
        CancellationToken ct)
    {
        var nonPhotoColumns = entity.Fields
            .Where(f => f.Kind != SyncFieldKind.Photo)
            .Select(f => f.Column)
            .ToArray();

        var selectCols = string.Join(", ", nonPhotoColumns.Select(QuoteIdentifier));
        var orderCols = string.Join(", ", entity.KeyColumns.Select(QuoteIdentifier));

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        string sql;
        if (string.Equals(entity.PublicName, "app_setting", StringComparison.Ordinal))
        {
            var keys = SyncCatalog.TravellingSettingKeys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var paramNames = new string[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                paramNames[i] = $"@k{i}";
                cmd.Parameters.AddWithValue(paramNames[i], keys[i]);
            }

            sql = $"SELECT {selectCols} FROM {QuoteIdentifier(entity.Table)} WHERE \"key\" IN ({string.Join(", ", paramNames)}) ORDER BY {orderCols}";
        }
        else
        {
            sql = $"SELECT {selectCols} FROM {QuoteIdentifier(entity.Table)} ORDER BY {orderCols}";
        }

#pragma warning disable CA2100 // SQL built exclusively from frozen SyncCatalog table/column identifiers
        cmd.CommandText = sql;
#pragma warning restore CA2100

        var rows = new List<SyncRow>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var dbRow = new Dictionary<string, object?>(nonPhotoColumns.Length, StringComparer.Ordinal);
                for (var i = 0; i < nonPhotoColumns.Length; i++)
                {
                    dbRow[nonPhotoColumns[i]] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
                        ? null
                        : reader.GetValue(i);
                }

                rows.Add(codec.ToPublic(entity, dbRow));
            }
        }

        var timedFields = SyncCatalog.TimedFields(entity);
        if (rows.Count > 0 && timedFields.Count > 0)
        {
            var fieldMsById = await ReadEntityFieldMsAsync(conn, tx, entity.PublicName, ct).ConfigureAwait(false);
            if (fieldMsById.Count > 0)
            {
                var timedNames = timedFields.Select(f => f.PublicName).ToHashSet(StringComparer.Ordinal);
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (!fieldMsById.TryGetValue(row.Id, out var rawMap))
                        continue;

                    var rowMs = row.Fields.TryGetValue("updated_at_ms", out var msEl) && msEl.TryGetInt64(out var parsedMs)
                        ? parsedMs
                        : long.MaxValue;
                    var filtered = new Dictionary<string, long>(StringComparer.Ordinal);
                    foreach (var (field, ms) in rawMap)
                    {
                        if (timedNames.Contains(field) && ms >= 0 && ms < rowMs)
                            filtered[field] = ms;
                    }

                    if (filtered.Count > 0)
                        rows[i] = row with { FieldMs = filtered };
                }
            }
        }

        rows.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        return rows;
    }

    private static async Task<Dictionary<string, Dictionary<string, long>>> ReadEntityFieldMsAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        string entityPublicName,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT \"entity_id\", \"field\", \"ms\" FROM \"sync_field_ms\" WHERE \"entity\" = @e";
        cmd.Parameters.AddWithValue("@e", entityPublicName);

        var map = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var field = reader.GetString(1);
            var ms = reader.GetInt64(2);
            if (!map.TryGetValue(id, out var rowMap))
            {
                rowMap = new Dictionary<string, long>(StringComparer.Ordinal);
                map[id] = rowMap;
            }

            rowMap[field] = ms;
        }

        return map;
    }

    private static async Task WriteEntityJsonLinesAsync(
        Stream output,
        List<SyncRow> rows,
        CancellationToken ct)
    {
        foreach (var row in rows)
        {
            await using (var writer = new Utf8JsonWriter(output, WriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteString("id", row.Id);

                foreach (var (key, element) in row.Fields)
                {
                    if (string.Equals(key, "id", StringComparison.Ordinal))
                        continue;

                    writer.WritePropertyName(key);
                    element.WriteTo(writer);
                }

                if (row.FieldMs.Count > 0)
                {
                    writer.WritePropertyName("_field_ms");
                    writer.WriteStartObject();
                    foreach (var fieldName in row.FieldMs.Keys.Order(StringComparer.Ordinal))
                    {
                        writer.WriteNumber(fieldName, row.FieldMs[fieldName]);
                    }
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }

            await output.WriteAsync(NewLine, ct).ConfigureAwait(false);
        }
    }

    private static async Task<List<SyncTombstoneRow>> ReadTombstonesAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT \"entity\", \"entity_id\", \"deleted_at_ms\", \"device_id\", \"alias_of\" FROM \"sync_tombstone\" ORDER BY \"entity\", \"entity_id\"";

        var list = new List<SyncTombstoneRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var entity = reader.GetString(0);
            var entityId = reader.GetString(1);
            var deletedAtMs = reader.GetInt64(2);
            var deviceId = await reader.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : reader.GetString(3);
            var aliasOf = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetString(4);
            list.Add(new SyncTombstoneRow(entity, entityId, deletedAtMs, deviceId, aliasOf));
        }

        list.Sort(static (a, b) =>
        {
            var cmp = StringComparer.Ordinal.Compare(a.Entity, b.Entity);
            return cmp != 0 ? cmp : StringComparer.Ordinal.Compare(a.EntityId, b.EntityId);
        });
        return list;
    }

    private static async Task WriteTombstonesJsonLinesAsync(
        Stream output,
        List<SyncTombstoneRow> tombstones,
        CancellationToken ct)
    {
        foreach (var t in tombstones)
        {
            await JsonSerializer.SerializeAsync(output, t, JsonOptions, ct).ConfigureAwait(false);
            await output.WriteAsync(NewLine, ct).ConfigureAwait(false);
        }
    }

    private static async Task WritePhotosAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        ZipArchive archive,
        bool includePhotos,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = includePhotos
            ? "SELECT \"id\", \"mime_type\", \"content\", \"thumbnail\" FROM \"attachment\" ORDER BY \"id\""
            : "SELECT \"id\", \"thumbnail\" FROM \"attachment\" ORDER BY \"id\"";

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            if (includePhotos)
            {
                var mimeType = reader.GetString(1);
                var ext = string.Equals(mimeType, "image/png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";

                if (!await reader.IsDBNullAsync(2, ct).ConfigureAwait(false))
                {
                    await using var contentStream = reader.GetStream(2);
                    var contentEntry = archive.CreateEntry($"photos/{id}.{ext}", CompressionLevel.NoCompression);
                    await using var entryOut = contentEntry.Open();
                    await contentStream.CopyToAsync(entryOut, ct).ConfigureAwait(false);
                }

                if (!await reader.IsDBNullAsync(3, ct).ConfigureAwait(false))
                {
                    await using var thumbStream = reader.GetStream(3);
                    var thumbEntry = archive.CreateEntry($"photos/{id}.thumb.jpg", CompressionLevel.NoCompression);
                    await using var entryOut = thumbEntry.Open();
                    await thumbStream.CopyToAsync(entryOut, ct).ConfigureAwait(false);
                }
            }
            else
            {
                if (!await reader.IsDBNullAsync(1, ct).ConfigureAwait(false))
                {
                    await using var thumbStream = reader.GetStream(1);
                    var thumbEntry = archive.CreateEntry($"photos/{id}.thumb.jpg", CompressionLevel.NoCompression);
                    await using var entryOut = thumbEntry.Open();
                    await thumbStream.CopyToAsync(entryOut, ct).ConfigureAwait(false);
                }
            }
        }
    }

    private static string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
}
