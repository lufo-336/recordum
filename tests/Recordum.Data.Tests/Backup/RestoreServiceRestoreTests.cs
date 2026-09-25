#pragma warning disable CA1416   // Windows-only (DpapiKeyProvider in EncryptedDbFixture)

using System.Runtime.Versioning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

[SupportedOSPlatform("windows")]
[Trait("Category", "WindowsOnly")]
public class RestoreServiceRestoreTests
{
    private const string Pass = "passphrase-1234567890";

    [Fact]
    public async Task Restore_round_trip_recovers_counts_and_writes_pre_restore_snapshot()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            int habitsBefore;
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                habitsBefore = await db.HabitNodes.IgnoreQueryFilters().CountAsync();

            var bk = await fixture.CreateBackupAsync(Pass);

            // Wipe a few rows to make restore observable
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                await db.HabitNodes.IgnoreQueryFilters().Take(2).ExecuteDeleteAsync();
            }

            var restore = fixture.NewRestoreService();
            var result = await restore.RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: false));

            result.UnavailablePlugins.Should().BeEmpty();

            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var habitsAfter = await db.HabitNodes.IgnoreQueryFilters().CountAsync();
                habitsAfter.Should().Be(habitsBefore);
            }

            // Safety snapshot exists
            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "pre-restore-*.recbak")
                     .Should().NotBeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Restore_with_SkipSafetySnapshot_does_not_create_pre_restore()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bk = await fixture.CreateBackupAsync(Pass);
            foreach (var f in Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "pre-restore-*"))
                File.Delete(f);

            var restore = fixture.NewRestoreService();
            await restore.RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: true));

            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "pre-restore-*").Should().BeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }
}
