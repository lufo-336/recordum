using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recordum.Data.Sync;

public sealed record SyncManifest(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("format_version")] string FormatVersion,
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("device_name")] string DeviceName,
    [property: JsonPropertyName("app_version")] string AppVersion,
    [property: JsonPropertyName("written_at_ms")] long WrittenAtMs,
    [property: JsonPropertyName("includes_photos")] bool IncludesPhotos,
    [property: JsonPropertyName("entities")] IReadOnlyDictionary<string, int> Entities)
{
    /// <summary>Per ogni pari, il <c>written_at_ms</c> dell'ultimo suo file unito qui prima di scrivere questo
    /// (formato §2.1). Assente nel file = vuoto.</summary>
    [JsonPropertyName("merged_from")]
    public IReadOnlyDictionary<string, long> MergedFrom { get; init; } = new Dictionary<string, long>();
}

public sealed record SyncRow(
    string Id,
    IReadOnlyDictionary<string, JsonElement> Fields)
{
    /// <summary>Ruling 20: l'ora dei campi più vecchi della riga (<c>_field_ms</c>, formato §2.2). Un campo assente ha
    /// l'ora della riga (<c>updated_at_ms</c>).</summary>
    public IReadOnlyDictionary<string, long> FieldMs { get; init; } = new Dictionary<string, long>(StringComparer.Ordinal);
}

public sealed record SyncTombstoneRow(
    [property: JsonPropertyName("entity")] string Entity,
    [property: JsonPropertyName("entity_id")] string EntityId,
    [property: JsonPropertyName("deleted_at_ms")] long DeletedAtMs,
    [property: JsonPropertyName("device_id")] string? DeviceId,
    [property: JsonPropertyName("alias_of")] string? AliasOf);

/// <summary>
/// Istantanea validata di un archivio di scambio <c>recordum-sync</c>.
/// Le righe JSON e le lapidi sono tutte caricate e validate all'apertura;
/// le foto vengono aperte su richiesta dallo <see cref="ZipArchive"/> sottostante fino al <see cref="Dispose"/>.
/// </summary>
public sealed class SyncSnapshot : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _photoEntries;
    private bool _disposed;

    internal SyncSnapshot(
        ZipArchive archive,
        SyncManifest manifest,
        IReadOnlyDictionary<string, IReadOnlyList<SyncRow>> rows,
        IReadOnlyList<SyncTombstoneRow> tombstones)
    {
        _archive = archive;
        Manifest = manifest;
        Rows = rows;
        Tombstones = tombstones;

        _photoEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("photos/", StringComparison.Ordinal))
            {
                _photoEntries[entry.FullName] = entry;
            }
        }
    }

    public SyncManifest Manifest { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<SyncRow>> Rows { get; }

    public IReadOnlyList<SyncTombstoneRow> Tombstones { get; }

    public Stream? OpenPhoto(string attachmentId, bool thumbnail)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

        if (thumbnail)
        {
            return _photoEntries.TryGetValue($"photos/{attachmentId}.thumb.jpg", out var thumbEntry)
                ? thumbEntry.Open()
                : null;
        }

        if (_photoEntries.TryGetValue($"photos/{attachmentId}.jpg", out var jpgEntry))
            return jpgEntry.Open();

        if (_photoEntries.TryGetValue($"photos/{attachmentId}.png", out var pngEntry))
            return pngEntry.Open();

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _archive.Dispose();
    }
}
