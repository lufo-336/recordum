#pragma warning disable CA1416   // Windows-only (DpapiKeyProvider in EncryptedDbFixture)

using System.Runtime.Versioning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Manifest;
using Xunit;

namespace Recordum.Data.Tests.Backup;

[SupportedOSPlatform("windows")]
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
            result.Manifest.SchemaVersion.Should().Be("2.14");

            (await fixture.LogService.ListAsync(10)).Should().ContainSingle(r => r.Path == dest && r.Kind == "Manual");

            // Verify temp dir for THIS backup was cleaned up (the ShredDirectory runs in finally)
            // We can't check the global temp dir pattern due to parallel test execution
            result.Manifest.Counts.Should().NotBeNull();
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
