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

    [Fact]
    public async Task Preserves_per_note_subfolder_structure_for_attachments()
    {
        // Photos live under attachments/{noteId}/{id}.{ext}, not flat — the zip entry must carry
        // that structure or the reader has nothing to reconstruct the note folder from.
        var tmp = Directory.CreateTempSubdirectory("rec-bk-writer-nested-");
        try
        {
            var dbFile = Path.Combine(tmp.FullName, "recordum.db");
            await File.WriteAllBytesAsync(dbFile, "PRETEND-SQLITE"u8.ToArray());
            var att = Path.Combine(tmp.FullName, "attachments");
            var noteDir = Path.Combine(att, "11111111-1111-1111-1111-111111111111");
            Directory.CreateDirectory(noteDir);
            await File.WriteAllBytesAsync(Path.Combine(noteDir, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg"), "PHOTO"u8.ToArray());

            var manifest = new BackupManifest(
                "2.13", "3.10.0", DateTime.UtcNow, BackupKind.Manual, 12,
                new BackupCounts(1, 2, 3, 1),
                Array.Empty<PluginManifestSnapshot>(),
                new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));

            var dest = Path.Combine(tmp.FullName, "out.zip");
            await using (var fs = File.Create(dest))
                await new BackupArchiveWriter().WriteAsync(fs, manifest, dbFile, att);

            using var zip = ZipFile.OpenRead(dest);
            zip.Entries.Select(e => e.FullName).Should().Contain(
                "attachments/11111111-1111-1111-1111-111111111111/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg");
        }
        finally { Directory.Delete(tmp.FullName, recursive: true); }
    }
}
