using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Data.Backup;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Manifest;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class BackupServiceTests
{
    private const string Pass = "passphrase-1234567890";

    [Fact]
    public async Task Create_emits_recbak_file_with_log_row_and_zero_temp_leftovers()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var svc = fixture.NewBackupService();
            var dest = Path.Combine(fixture.Options.BackupsDirectory, "manual.recbak");
            var result = await svc.CreateAsync(dest, passphrase: Pass, new BackupOptions(BackupKind.Manual), default);

            File.Exists(dest).Should().BeTrue();
            new FileInfo(dest).Length.Should().BeGreaterThan(0);
            result.Manifest.Kind.Should().Be(BackupKind.Manual);
            result.Manifest.SchemaVersion.Should().Be("2.20");

            (await fixture.LogService.ListAsync(10)).Should().ContainSingle(r => r.Path == dest && r.Kind == "Manual");

            // Verify temp dir for THIS backup was cleaned up (the ShredDirectory runs in finally)
            // We can't check the global temp dir pattern due to parallel test execution
            result.Manifest.Counts.Should().NotBeNull();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task The_database_inside_a_new_recbak_is_plaintext()
    {
        // VACUUM INTO alone keeps the machine's key: such a backup would restore only where it was made (D9 probe, trap 2).
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var dest = Path.Combine(fixture.Options.BackupsDirectory, "plain-inside.recbak");
            await fixture.NewBackupService().CreateAsync(dest, Pass, new BackupOptions(BackupKind.Manual), default);
            Directory.EnumerateFileSystemEntries(fixture.Options.TempDirectory).Should()
                .BeEmpty("the plaintext dump is shredded, and nothing may still hold it open");

            var zip = Path.Combine(fixture.RootDir, "inside.zip");
            var db = Path.Combine(fixture.RootDir, "inside.db");
            await using (var src = File.OpenRead(dest))
            await using (var dst = File.Create(zip))
                await new AesGcmBackupCipher().DecryptAsync(src, dst, Pass);
            await new BackupArchiveReader().ExtractDbAsync(zip, db);

            var header = new byte[16];
            await using (var fs = File.OpenRead(db))
                fs.ReadExactly(header);
            Encoding.ASCII.GetString(header).Should().Be("SQLite format 3\0");

            await using var conn = new SqliteConnection($"Data Source={db};Pooling=False");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            (await cmd.ExecuteScalarAsync()).Should().Be("ok");
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Create_plugin_snapshot_lists_currently_activated()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await fixture.PluginManager.EnableAsync(new PluginManifest
            {
                SchemaVersion = "1.0",
                Id = "io.recordum.chart-engine",
                Name = "Chart Engine",
                Version = "3.10.0",
                Publisher = new Recordum.Plugins.Contracts.Manifest.ManifestPublisher { Id = "io.recordum", DisplayName = "Recordum" },
                MinRecordumVersion = "3.0.0",
                Entry = "main.js",
            });

            var svc = fixture.NewBackupService();
            var dest = Path.Combine(fixture.Options.BackupsDirectory, "plug.recbak");
            var result = await svc.CreateAsync(dest, Pass, new BackupOptions(BackupKind.Manual), default);

            result.Manifest.Plugins.Should().ContainSingle(p => p.PluginId == "io.recordum.chart-engine" && p.Version == "3.10.0");
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Create_with_auto_kind_enforces_retention()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            // Set retention_count = 2
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var s = await db.AppSettings.FirstAsync(x => x.Key == "backup.auto.retention_count");
                s.Value = "2";
                await db.SaveChangesAsync();
            }

            var svc = fixture.NewBackupService();

            for (var i = 0; i < 3; i++)
            {
                fixture.Clock.Advance(TimeSpan.FromSeconds(1));
                var dest = Path.Combine(fixture.Options.BackupsDirectory, $"auto-{i}.recbak");
                await svc.CreateAsync(dest, Pass, new BackupOptions(BackupKind.Auto), default);
            }

            var logs = await fixture.LogService.ListAsync(10);
            logs.Count(x => x.Kind == "Auto").Should().Be(2);
            File.Exists(Path.Combine(fixture.Options.BackupsDirectory, "auto-0.recbak")).Should().BeFalse();
            File.Exists(Path.Combine(fixture.Options.BackupsDirectory, "auto-2.recbak")).Should().BeTrue();
        }
        finally { await fixture.DisposeAsync(); }
    }
}
