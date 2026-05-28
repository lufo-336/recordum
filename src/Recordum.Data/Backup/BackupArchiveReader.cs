#pragma warning disable CA1822
using System.IO.Compression;
using System.Text.Json;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

public sealed class BackupArchiveReader
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<BackupManifest> ReadManifestAsync(string zipPath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("manifest.json")
            ?? throw new RestoreException("invalid_format", "manifest.json missing");
        await using var s = entry.Open();
        using var sr = new StreamReader(s);
        var json = await sr.ReadToEndAsync(ct).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json, Json)
            ?? throw new RestoreException("invalid_format", "manifest.json malformed");
        return manifest;
    }

    public async Task ExtractDbAsync(string zipPath, string destinationFile, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("db/recordum.db")
            ?? throw new RestoreException("invalid_format", "db/recordum.db missing");
        await using var src = entry.Open();
        await using var dst = File.Create(destinationFile);
        await src.CopyToAsync(dst, ct).ConfigureAwait(false);
    }

    public async Task ExtractAttachmentsAsync(string zipPath, string destinationDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDir);
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith("attachments/", StringComparison.Ordinal)) continue;
            if (entry.FullName.EndsWith('/')) continue;
            var name = Path.GetFileName(entry.FullName);
            var dst = Path.Combine(destinationDir, name);
            await using var src = entry.Open();
            await using var fs = File.Create(dst);
            await src.CopyToAsync(fs, ct).ConfigureAwait(false);
        }
    }
}
