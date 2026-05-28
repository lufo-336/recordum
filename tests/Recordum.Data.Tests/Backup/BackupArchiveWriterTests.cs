using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.Data.Backup;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class BackupArchiveWriterTests
{
    [Fact]
    public async Task Writes_manifest_db_and_attachments_to_zip()
    {
        var tmp = Directory.CreateTempSubdirectory("rec-bk-writer-");
        try
        {
            var dbFile = Path.Combine(tmp.FullName, "recordum.db");
            await File.WriteAllBytesAsync(dbFile, "PRETEND-SQLITE"u8.ToArray());
            var att = Path.Combine(tmp.FullName, "attachments");
            Directory.CreateDirectory(att);
            await File.WriteAllBytesAsync(Path.Combine(att, "a1.bin"), "X"u8.ToArray());

            var manifest = new BackupManifest(
                "2.7", "3.10.0", DateTime.UtcNow, BackupKind.Manual, 12,
                new BackupCounts(1, 2, 3, 1),
                Array.Empty<PluginManifestSnapshot>(),
                new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));

            var dest = Path.Combine(tmp.FullName, "out.zip");
            await using (var fs = File.Create(dest))
            {
                await new BackupArchiveWriter().WriteAsync(fs, manifest, dbFile, att);
            }

            using var zip = ZipFile.OpenRead(dest);
            zip.Entries.Select(e => e.FullName).Should().Contain(new[]
            {
                "manifest.json", "db/recordum.db", "attachments/a1.bin"
            });
            var json = new StreamReader(zip.GetEntry("manifest.json")!.Open()).ReadToEnd();
            JsonDocument.Parse(json).RootElement.GetProperty("schema_version").GetString().Should().Be("2.7");
        }
        finally { Directory.Delete(tmp.FullName, recursive: true); }
    }
}
