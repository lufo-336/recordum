#pragma warning disable CA1416   // Windows-only (DpapiKeyProvider in EncryptedDbFixture)

using System.Runtime.Versioning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

[SupportedOSPlatform("windows")]
public class BackupSchedulerTests
{
    private const string Pass = "passphrase-1234567890";

    [Fact]
    public async Task RunDueAsync_runs_when_overdue_and_updates_last_run()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            fixture.WriteAutoBackupKey(Pass);
            // last_run = empty (overdue)
            var sched = fixture.NewScheduler();
            await sched.RunDueAsync();

            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            var last = await db.AppSettings.AsNoTracking().FirstAsync(x => x.Key == "backup.auto.last_run_utc");
            last.Value.Should().NotBeEmpty();
            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "auto-*.recbak").Should().NotBeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task RunDueAsync_skips_when_not_due()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            fixture.WriteAutoBackupKey(Pass);
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var s = await db.AppSettings.FirstAsync(x => x.Key == "backup.auto.last_run_utc");
                s.Value = fixture.Clock.UtcNow.ToString("O");
                await db.SaveChangesAsync();
            }
            var sched = fixture.NewScheduler();
            await sched.RunDueAsync();

            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "auto-*.recbak").Should().BeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task RunDueAsync_self_disables_when_dpapi_key_missing()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            // Don't write the auto-backup key file → key missing
            var sched = fixture.NewScheduler();
            await sched.RunDueAsync();

            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            var enabled = await db.AppSettings.AsNoTracking().FirstAsync(x => x.Key == "backup.auto.enabled");
            enabled.Value.Should().Be("false");
        }
        finally { await fixture.DisposeAsync(); }
    }
}
