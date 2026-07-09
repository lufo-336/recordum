#pragma warning disable CA1822
using System.IO.Compression;
using System.Text.Json;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

public sealed class BackupArchiveWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public async Task WriteAsync(Stream destination, BackupManifest manifest, string dbFilePath, string attachmentsDir, CancellationToken ct = default)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var es = manifestEntry.Open())
        await using (var sw = new StreamWriter(es))
        {
            await sw.WriteAsync(JsonSerializer.Serialize(manifest, Json));
        }

        if (File.Exists(dbFilePath))
        {
            var dbEntry = zip.CreateEntry("db/recordum.db", CompressionLevel.Optimal);
            await using var es = dbEntry.Open();
            await using var fs = File.OpenRead(dbFilePath);
            await fs.CopyToAsync(es, ct).ConfigureAwait(false);
        }

        if (Directory.Exists(attachmentsDir))
        {
            // Recursive: photos are organized as attachments/{noteId}/{id}.{ext}, not flat —
            // the zip entry path must preserve that structure for the reader to restore it.
            foreach (var path in Directory.EnumerateFiles(attachmentsDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(attachmentsDir, path).Replace(Path.DirectorySeparatorChar, '/');
                var entry = zip.CreateEntry($"attachments/{rel}", CompressionLevel.Optimal);
                await using var es = entry.Open();
                await using var fs = File.OpenRead(path);
                await fs.CopyToAsync(es, ct).ConfigureAwait(false);
            }
        }
    }
}
