using System.IO.Compression;
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

    [Fact]
    public async Task ExtractAttachments_preserves_per_note_subfolder_structure()
    {
        var tmp = Directory.CreateTempSubdirectory("rec-bk-reader-nested-");
        try
        {
            var dbFile = Path.Combine(tmp.FullName, "recordum.db");
            await File.WriteAllBytesAsync(dbFile, "DBBYTES"u8.ToArray());
            var att = Path.Combine(tmp.FullName, "attachments");
            var noteDir = Path.Combine(att, "22222222-2222-2222-2222-222222222222");
            Directory.CreateDirectory(noteDir);
            await File.WriteAllBytesAsync(Path.Combine(noteDir, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg"), "PHOTO"u8.ToArray());
            var manifest = new BackupManifest("2.13", "3.10.0", DateTime.UtcNow, BackupKind.Manual, 7,
                new BackupCounts(1, 1, 0, 1), Array.Empty<PluginManifestSnapshot>(),
                new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));
            var zipPath = Path.Combine(tmp.FullName, "in.zip");
            await using (var fs = File.Create(zipPath))
                await new BackupArchiveWriter().WriteAsync(fs, manifest, dbFile, att);

            var outAtt = Path.Combine(tmp.FullName, "extracted_att");
            var reader = new BackupArchiveReader();
            await reader.ExtractAttachmentsAsync(zipPath, outAtt);

            var restored = Path.Combine(outAtt, "22222222-2222-2222-2222-222222222222", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg");
            File.Exists(restored).Should().BeTrue();
            File.ReadAllBytes(restored).Should().Equal("PHOTO"u8.ToArray());
        }
        finally { Directory.Delete(tmp.FullName, recursive: true); }
    }

    [Fact]
    public async Task ExtractAttachments_rejects_zip_slip_path_traversal_entries()
    {
        var tmp = Directory.CreateTempSubdirectory("rec-bk-reader-zipslip-");
        try
        {
            var zipPath = Path.Combine(tmp.FullName, "malicious.zip");
            await using (var fs = File.Create(zipPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // Attempts to escape destinationDir via a ".." segment inside the attachments/ prefix.
                var evil = zip.CreateEntry("attachments/../../evil.txt");
                await using var es = evil.Open();
                await using var sw = new StreamWriter(es);
                await sw.WriteAsync("payload");
            }

            var outAtt = Path.Combine(tmp.FullName, "extracted_att");
            var reader = new BackupArchiveReader();
            await reader.ExtractAttachmentsAsync(zipPath, outAtt);

            File.Exists(Path.Combine(tmp.FullName, "evil.txt")).Should().BeFalse();
            Directory.Exists(outAtt).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(outAtt).Should().BeEmpty();
        }
        finally { Directory.Delete(tmp.FullName, recursive: true); }
    }
}
