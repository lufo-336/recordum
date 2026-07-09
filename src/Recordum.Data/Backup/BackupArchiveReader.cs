#pragma warning disable CA1822
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

public sealed class BackupArchiveReader
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // CR3: a malicious .recbak is an untrusted zip — cap how many bytes we will write out for the
    // restored DB and (cumulatively) for attachments, so a decompression bomb cannot fill the disk.
    private const long MaxExtractBytes = 4L * 1024 * 1024 * 1024;  // 4 GiB

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
        await CopyBoundedAsync(src, dst, MaxExtractBytes, ct).ConfigureAwait(false);
    }

    public async Task ExtractAttachmentsAsync(string zipPath, string destinationDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDir);
        var destinationRoot = Path.GetFullPath(destinationDir);
        using var zip = ZipFile.OpenRead(zipPath);
        var remaining = MaxExtractBytes;
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith("attachments/", StringComparison.Ordinal)) continue;
            if (entry.FullName.EndsWith('/')) continue;

            // Photos are organized as attachments/{noteId}/{id}.{ext} — preserve that relative
            // structure (needed for AttachmentService to find the file again after restore)
            // while still defeating zip-slip: reject any segment that isn't a plain name
            // (no "..", no rooted/drive segment), then verify the resolved path still lands
            // inside destinationRoot before writing anything.
            var relative = entry.FullName["attachments/".Length..];
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(IsUnsafeSegment)) continue;

            var dst = Path.GetFullPath(Path.Combine([destinationRoot, .. segments]));
            if (!dst.StartsWith(destinationRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            await using var src = entry.Open();
            await using var fs = File.Create(dst);
            remaining -= await CopyBoundedAsync(src, fs, remaining, ct).ConfigureAwait(false);
        }
    }

    private static bool IsUnsafeSegment(string segment) =>
        segment is ".." or "." || segment.Contains(':') || segment.Contains('\\') || Path.IsPathRooted(segment);

    // CR3: copy at most <paramref name="maxBytes"/> from an untrusted zip entry, returning the count
    // copied so callers can enforce a cumulative budget across many entries.
    private static async Task<long> CopyBoundedAsync(Stream src, Stream dst, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new RestoreException("invalid_format", "Backup archive exceeds the maximum supported size.");
            await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
        return total;
    }
}
