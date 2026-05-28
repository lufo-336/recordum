using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.Data.Backup;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class BackupArchiveReaderTests
{
    [Fact]
    public async Task Reads_manifest_and_extracts_db_and_attachments()
    {
        var tmp = Directory.CreateTempSubdirectory("rec-bk-reader-");
        try
        {
            // Build a zip via the writer
            var dbFile = Path.Combine(tmp.FullName, "recordum.db");
            await File.WriteAllBytesAsync(dbFile, "DBBYTES"u8.ToArray());
            var att = Path.Combine(tmp.FullName, "attachments");
            Directory.CreateDirectory(att);
            await File.WriteAllBytesAsync(Path.Combine(att, "x.bin"), "ATT"u8.ToArray());
            var manifest = new BackupManifest("2.7", "3.10.0", DateTime.UtcNow, BackupKind.Manual, 7,
                new BackupCounts(1, 1, 0, 1), Array.Empty<PluginManifestSnapshot>(),
                new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));
            var zipPath = Path.Combine(tmp.FullName, "in.zip");
            await using (var fs = File.Create(zipPath))
                await new BackupArchiveWriter().WriteAsync(fs, manifest, dbFile, att);

            var outDb = Path.Combine(tmp.FullName, "extracted.db");
            var outAtt = Path.Combine(tmp.FullName, "extracted_att");
            Directory.CreateDirectory(outAtt);

            var reader = new BackupArchiveReader();
            var loaded = await reader.ReadManifestAsync(zipPath);
            loaded.SchemaVersion.Should().Be("2.7");

            await reader.ExtractDbAsync(zipPath, outDb);
            File.ReadAllBytes(outDb).Should().Equal("DBBYTES"u8.ToArray());

            await reader.ExtractAttachmentsAsync(zipPath, outAtt);
            File.ReadAllBytes(Path.Combine(outAtt, "x.bin")).Should().Equal("ATT"u8.ToArray());
        }
        finally { Directory.Delete(tmp.FullName, recursive: true); }
    }
}
