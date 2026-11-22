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
    private const int SupportedMajorVersion = 1;

    public static SyncSnapshot Open(Stream zipIn)
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
            var manifest = ReadAndValidateManifest(archive);
            var rows = ReadAllEntityRows(archive);
            var tombstones = ReadTombstones(archive);
            return new SyncSnapshot(archive, manifest, rows, tombstones);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private static SyncManifest ReadAndValidateManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json")
            ?? throw new SyncFormatException("Missing manifest.json in sync archive.");

        SyncManifest? manifest;
        try
        {
            using var stream = entry.Open();
            manifest = JsonSerializer.Deserialize<SyncManifest>(stream);
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

        if (string.IsNullOrWhiteSpace(manifest.DeviceId))
            throw new SyncFormatException("Manifest device_id is required.");

        if (manifest.Entities is null)
            throw new SyncFormatException("Manifest entities map is required.");

        return manifest;
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
            throw new SyncFormatException($"Unsupported major format_version '{formatVersion}'.");
    }

    private static Dictionary<string, IReadOnlyList<SyncRow>> ReadAllEntityRows(ZipArchive archive)
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

            var parsedRows = ParseEntityJsonLines(entry, keepIdInFields: !isKnown || hasIdField);
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

    private static List<SyncRow> ParseEntityJsonLines(ZipArchiveEntry entry, bool keepIdInFields)
    {
        var rows = new List<SyncRow>();
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

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

                    fields[prop.Name] = prop.Value.Clone();
                }

                rows.Add(new SyncRow(id, fields));
            }
        }

        return rows;
    }

    private static List<SyncTombstoneRow> ReadTombstones(ZipArchive archive)
    {
        var entry = archive.GetEntry("tombstones.jsonl");
        if (entry is null)
            return [];

        var tombstones = new List<SyncTombstoneRow>();
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

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
                var deviceId = GetOptionalString(root, "device_id", lineNumber);
                var aliasOf = GetOptionalString(root, "alias_of", lineNumber);

                tombstones.Add(new SyncTombstoneRow(entityName, entityId, deletedAtMs, deviceId, aliasOf));
            }
        }

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
}
