using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Recordum.Data.Sync;

/// <summary>
/// Apre e valida un archivio ZIP <c>recordum-sync</c>.
/// Tutte le righe JSON e le lapidi vengono caricate e validate all'apertura (nessuna lettura parziale);
/// lo <see cref="ZipArchive"/> resta aperto dentro <see cref="SyncSnapshot"/> per servire le foto su richiesta.
/// </summary>
public static class SyncArchiveReader
{
    /// <summary>L'ora più alta accettata (l'ultimo millisecondo dell'anno 9999): un'ora vicina a <c>long.MaxValue</c> farebbe
    /// traboccare il "+ 1" dell'unione e dei trigger (SQLite passa a REAL). Il file arriva cifrato con la frase, quindi
    /// da un pari fidato: è un dato rovinato, non un attacco. ponytail: un'ora lontana ma valida (anno 3000) fissa la
    /// riga finché nessuno la supera; è il limite di ogni LWW con l'orologio di chi scrive.</summary>
    internal const long MaxMs = 253_402_300_799_999; // DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
    internal const long MaxManifestBytes = 1L * 1024 * 1024;
    internal const long MaxExtractBytes = 4L * 1024 * 1024 * 1024;

    private const int SupportedMajorVersion = 1;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static SyncSnapshot Open(Stream zipIn) => Open(zipIn, MaxExtractBytes);

    internal static SyncSnapshot Open(Stream zipIn, long maxExtractBytes)
    {
        ArgumentNullException.ThrowIfNull(zipIn);
        if (!zipIn.CanSeek)
            throw new SyncFormatException("Sync archive stream must be seekable.");

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipIn, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            throw new SyncFormatException("Invalid sync ZIP archive.", ex);
        }

        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in archive.Entries)
            {
                if (!seen.Add(e.FullName))
                    throw new SyncFormatException($"Duplicate entry '{e.FullName}' in sync archive.");
            }

            var remaining = maxExtractBytes;
            var manifest = ReadAndValidateManifest(archive, ref remaining);
            var rows = ReadAllEntityRows(archive, ref remaining);
            var tombstones = ReadTombstones(archive, ref remaining);
            return new SyncSnapshot(archive, manifest, rows, tombstones);
        }
        catch (DecoderFallbackException ex)
        {
            archive.Dispose();
            throw new SyncFormatException("Invalid UTF-8 in sync archive.", ex);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private static SyncManifest ReadAndValidateManifest(ZipArchive archive, ref long remaining)
    {
        var entry = archive.GetEntry("manifest.json")
            ?? throw new SyncFormatException("Missing manifest.json in sync archive.");

        SyncManifest? manifest;
        try
        {
            using var stream = new BoundedReadStream(entry.Open(), Math.Min(MaxManifestBytes, remaining));
            manifest = JsonSerializer.Deserialize<SyncManifest>(stream);
            remaining -= stream.BytesRead;
        }
        catch (JsonException ex)
        {
            throw new SyncFormatException("Malformed manifest.json.", ex);
        }

        if (manifest is null)
            throw new SyncFormatException("Empty manifest.json.");

        if (!string.Equals(manifest.Format, SyncArchiveWriter.FormatName, StringComparison.Ordinal))
            throw new SyncFormatException($"Unsupported sync format '{manifest.Format}'.");

        ValidateFormatVersion(manifest.FormatVersion);

        if (manifest.WrittenAtMs is < 0 or > MaxMs)
            throw new SyncFormatException($"Invalid manifest 'written_at_ms': {manifest.WrittenAtMs} is outside 0..{MaxMs}.");

        if (string.IsNullOrWhiteSpace(manifest.DeviceId))
            throw new SyncFormatException("Manifest device_id is required.");

        if (manifest.Entities is null)
            throw new SyncFormatException("Manifest entities map is required.");

        // "merged_from": null o assente = nessun file unito.
        return manifest.MergedFrom is null ? manifest with { MergedFrom = new Dictionary<string, long>() } : manifest;
    }

    private static void ValidateFormatVersion(string? formatVersion)
    {
        if (string.IsNullOrWhiteSpace(formatVersion))
            throw new SyncFormatException("Missing format_version in manifest.json.");

        var parts = formatVersion.Split('.');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new SyncFormatException($"Invalid format_version '{formatVersion}'.");
        }

        if (major != SupportedMajorVersion)
            throw new SyncFormatException(
                $"Unsupported major format_version '{formatVersion}'.",
                isNewerMajorVersion: major > SupportedMajorVersion);
    }

    private static Dictionary<string, IReadOnlyList<SyncRow>> ReadAllEntityRows(ZipArchive archive, ref long remaining)
    {
        var knownEntities = SyncCatalog.Entities.ToDictionary(e => e.PublicName, StringComparer.Ordinal);
        var result = new Dictionary<string, IReadOnlyList<SyncRow>>(knownEntities.Count, StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("data/", StringComparison.Ordinal)
                || !entry.FullName.EndsWith(".jsonl", StringComparison.Ordinal))
            {
                continue;
            }

            var publicName = entry.FullName["data/".Length..^".jsonl".Length];
            var isKnown = knownEntities.TryGetValue(publicName, out var entity);
            var hasIdField = isKnown && entity!.Fields.Any(f => string.Equals(f.PublicName, "id", StringComparison.Ordinal));

            var parsedRows = ParseEntityJsonLines(entry, keepIdInFields: !isKnown || hasIdField, isKnown ? entity : null, ref remaining);
            if (isKnown)
            {
                result[publicName] = parsedRows;
            }
        }

        foreach (var entity in SyncCatalog.Entities)
        {
            if (!result.ContainsKey(entity.PublicName))
                result[entity.PublicName] = [];
        }

        return result;
    }

    private static List<SyncRow> ParseEntityJsonLines(ZipArchiveEntry entry, bool keepIdInFields, SyncEntity? entity, ref long remaining)
    {
        var rows = new List<SyncRow>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var timedFieldNames = entity is null
            ? null
            : SyncCatalog.TimedFields(entity).Select(f => f.PublicName).ToHashSet(StringComparer.Ordinal);
        using var stream = new BoundedReadStream(entry.Open(), remaining);
        using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false);

        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new SyncFormatException($"Malformed JSON in '{entry.FullName}' at line {lineNumber}.", ex);
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new SyncFormatException($"Expected JSON object in '{entry.FullName}' at line {lineNumber}.");

                if (!root.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                    throw new SyncFormatException($"Missing string 'id' in '{entry.FullName}' at line {lineNumber}.");

                var id = idProp.GetString();
                if (string.IsNullOrEmpty(id))
                    throw new SyncFormatException($"Empty 'id' in '{entry.FullName}' at line {lineNumber}.");

                var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var prop in root.EnumerateObject())
                {
                    if (!keepIdInFields && string.Equals(prop.Name, "id", StringComparison.Ordinal))
                        continue;
                    if (string.Equals(prop.Name, "_field_ms", StringComparison.Ordinal))
                        continue;

                    fields[prop.Name] = prop.Value.Clone();
                }

                if (entity is not null)
                {
                    if (!root.TryGetProperty("updated_at_ms", out var msProp)
                        || msProp.ValueKind != JsonValueKind.Number
                        || !msProp.TryGetInt64(out var rowMs)
                        || rowMs is < 0 or > MaxMs)
                    {
                        throw new SyncFormatException($"Missing or invalid 'updated_at_ms' in '{entry.FullName}' at line {lineNumber}.");
                    }
                }
                else if (root.TryGetProperty("updated_at_ms", out var msProp) && msProp.ValueKind == JsonValueKind.Number
                    && (!msProp.TryGetInt64(out var rowMs) || rowMs is < 0 or > MaxMs))
                {
                    throw new SyncFormatException($"Invalid 'updated_at_ms' in '{entry.FullName}' at line {lineNumber}: outside 0..{MaxMs}.");
                }

                var fieldMs = ReadFieldMs(root, timedFieldNames, entry.FullName, lineNumber);

                if (!ids.Add(id))
                    throw new SyncFormatException($"Duplicate id '{id}' in '{entry.FullName}' at line {lineNumber}.");

                if (entity is not null)
                    CheckIdMatchesKey(entity, id, root, entry.FullName, lineNumber);

                rows.Add(new SyncRow(id, fields) { FieldMs = fieldMs });
            }
        }

        remaining -= stream.BytesRead;
        return rows;
    }

    private static Dictionary<string, long> ReadFieldMs(
        JsonElement root,
        HashSet<string>? timedFieldNames,
        string file,
        int lineNumber)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!root.TryGetProperty("_field_ms", out var fieldMsProp))
            return result;

        if (fieldMsProp.ValueKind != JsonValueKind.Object)
            throw new SyncFormatException($"Invalid '_field_ms' in '{file}' at line {lineNumber}: expected JSON object.");

        long? rowMs = root.TryGetProperty("updated_at_ms", out var rowMsProp)
            && rowMsProp.ValueKind == JsonValueKind.Number
            && rowMsProp.TryGetInt64(out var parsedRowMs)
            ? parsedRowMs
            : null;

        foreach (var prop in fieldMsProp.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt64(out var ms) || ms < 0)
                throw new SyncFormatException($"Invalid '_field_ms.{prop.Name}' in '{file}' at line {lineNumber}: expected non-negative integer.");

            if (rowMs is null)
                throw new SyncFormatException($"Missing integer 'updated_at_ms' for '_field_ms' in '{file}' at line {lineNumber}.");

            if (ms > rowMs.Value)
                throw new SyncFormatException($"Invalid '_field_ms.{prop.Name}' in '{file}' at line {lineNumber}: timestamp {ms} exceeds 'updated_at_ms' ({rowMs.Value}).");

            // Un'ora uguale a quella della riga è la stessa cosa di nessuna ora: non entra.
            if (timedFieldNames is not null && timedFieldNames.Contains(prop.Name) && ms < rowMs.Value)
                result[prop.Name] = ms;
        }

        return result;
    }

    // L'id di riga e' la chiave composta dai valori dei campi chiave uniti con '|'.
    private static void CheckIdMatchesKey(SyncEntity entity, string id, JsonElement root, string file, int lineNumber)
    {
        var parts = new string[entity.KeyColumns.Count];
        for (var i = 0; i < parts.Length; i++)
        {
            var publicName = entity.Fields.First(f => f.Column == entity.KeyColumns[i]).PublicName;
            if (!root.TryGetProperty(publicName, out var v))
                throw new SyncFormatException($"Missing key field '{publicName}' in '{file}' at line {lineNumber}.");

            parts[i] = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString()!,
                JsonValueKind.Number => v.GetRawText(),
                _ => throw new SyncFormatException($"Invalid key field '{publicName}' in '{file}' at line {lineNumber}."),
            };
        }

        if (!string.Equals(string.Join('|', parts), id, StringComparison.Ordinal))
            throw new SyncFormatException($"Row id '{id}' does not match its key fields in '{file}' at line {lineNumber}.");
    }

    private static List<SyncTombstoneRow> ReadTombstones(ZipArchive archive, ref long remaining)
    {
        var entry = archive.GetEntry("tombstones.jsonl");
        if (entry is null)
            return [];

        var tombstones = new List<SyncTombstoneRow>();
        using var stream = new BoundedReadStream(entry.Open(), remaining);
        using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false);

        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new SyncFormatException($"Malformed JSON in 'tombstones.jsonl' at line {lineNumber}.", ex);
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new SyncFormatException($"Expected JSON object in 'tombstones.jsonl' at line {lineNumber}.");

                var entityName = GetRequiredString(root, "entity", lineNumber);
                var entityId = GetRequiredString(root, "entity_id", lineNumber);
                var deletedAtMs = GetRequiredInt64(root, "deleted_at_ms", lineNumber);
                if (deletedAtMs is < 0 or > MaxMs)
                    throw new SyncFormatException($"Invalid 'deleted_at_ms' in 'tombstones.jsonl' at line {lineNumber}: outside 0..{MaxMs}.");
                var deviceId = GetOptionalString(root, "device_id", lineNumber);
                var aliasOf = GetOptionalString(root, "alias_of", lineNumber);

                tombstones.Add(new SyncTombstoneRow(entityName, entityId, deletedAtMs, deviceId, aliasOf));
            }
        }

        remaining -= stream.BytesRead;
        return tombstones;
    }

    private static string GetRequiredString(JsonElement obj, string propertyName, int lineNumber)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new SyncFormatException($"Missing or invalid '{propertyName}' in 'tombstones.jsonl' at line {lineNumber}.");

        var value = prop.GetString();
        if (string.IsNullOrEmpty(value))
            throw new SyncFormatException($"Empty '{propertyName}' in 'tombstones.jsonl' at line {lineNumber}.");

        return value;
    }

    private static long GetRequiredInt64(JsonElement obj, string propertyName, int lineNumber)
    {
        if (!obj.TryGetProperty(propertyName, out var prop)
            || prop.ValueKind != JsonValueKind.Number
            || !prop.TryGetInt64(out var value))
        {
            throw new SyncFormatException($"Missing or invalid '{propertyName}' in 'tombstones.jsonl' at line {lineNumber}.");
        }

        return value;
    }

    private static string? GetOptionalString(JsonElement obj, string propertyName, int lineNumber)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        if (prop.ValueKind != JsonValueKind.String)
            throw new SyncFormatException($"Invalid '{propertyName}' in 'tombstones.jsonl' at line {lineNumber}.");

        return prop.GetString();
    }

    private sealed class BoundedReadStream(Stream inner, long maxBytes) : Stream
    {
        private long _bytesRead;

        public long BytesRead => _bytesRead;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _bytesRead;
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            Account(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            Account(read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            Account(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Account(read);
            return read;
        }

        private void Account(int read)
        {
            if (read <= 0)
                return;

            _bytesRead += read;
            if (_bytesRead > maxBytes)
                throw new SyncFormatException($"Sync archive decompressed data exceeds the maximum supported size ({maxBytes} bytes).");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
