using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

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

    [Fact]
    public async Task An_open_connection_makes_the_restore_busy_and_leaves_the_data_intact()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bk = await fixture.CreateBackupAsync(Pass);
            int remaining;
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                await db.HabitNodes.IgnoreQueryFilters().Take(2).ExecuteDeleteAsync();
                remaining = await db.HabitNodes.IgnoreQueryFilters().CountAsync();
            }

            await using (var holder = await fixture.DbFactory.CreateDbContextAsync())
            {
                await holder.Database.OpenConnectionAsync();
                var act = () => fixture.NewRestoreService(TimeSpan.FromMilliseconds(300))
                    .RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: true));
                (await act.Should().ThrowAsync<RestoreException>()).Which.Code.Should().Be("busy");
            }

            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                (await db.HabitNodes.IgnoreQueryFilters().CountAsync()).Should().Be(remaining, "il database vivo non è stato toccato");
            Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(fixture.Options.DatabasePath)!, ".restore-new-*")
                .Should().BeEmpty("la copia preparata si cancella");
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task After_a_successful_restore_no_other_user_writes_into_the_restored_database()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bk = await fixture.CreateBackupAsync(Pass);
            await fixture.NewRestoreService(keepGateClosed: true)
                .RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: true));

            // Come il tick dell'auto-backup che registra il proprio backup_log: resta in attesa fino all'uscita.
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var write = async () =>
            {
                await using var db = await fixture.DbFactory.CreateDbContextAsync(cts.Token);
                await db.AppSettings.Where(x => x.Key == "backup.auto.last_run_utc")
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Value, "2099-01-01T00:00:00Z"), cts.Token);
            };
            await write.Should().ThrowAsync<OperationCanceledException>();

            fixture.Gate.Reopen();
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                (await db.AppSettings.Where(x => x.Key == "backup.auto.last_run_utc").Select(x => x.Value).SingleOrDefaultAsync())
                    .Should().NotBe("2099-01-01T00:00:00Z");
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    [Trait("Category", "UnixOnly")]
    public async Task The_restored_database_is_private_at_once_not_at_the_next_start()
    {
        if (OperatingSystem.IsWindows()) return;
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var bk = await fixture.CreateBackupAsync(Pass);
            await fixture.NewRestoreService().RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: true));

            File.GetUnixFileMode(fixture.Options.DatabasePath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally { await fixture.DisposeAsync(); }
    }
}
