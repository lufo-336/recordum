using Recordum.WPF.Bootstrap;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Backup;
using Recordum.Data;
using Recordum.Presentation.Bootstrap;
using Xunit;

namespace Recordum.WPF.Tests.E2E;

[Trait("Category", "E2E")]
public class BackupRoundTripE2ETests
{
    [Fact]
    public async Task Full_pipeline_backup_then_restore_recovers_state()
    {
        var temp = Directory.CreateTempSubdirectory("rec-e2e-");
        try
        {
            // Build DI rooted at a temp folder
            var options = new RecordumOptions
            {
                DatabasePath = Path.Combine(temp.FullName, "recordum.db"),
                KeyFilePath = Path.Combine(temp.FullName, "db.key"),
                SaltFilePath = Path.Combine(temp.FullName, "db.salt"),
                AttachmentsDirectory = Path.Combine(temp.FullName, "attachments"),
                BackupsDirectory = Path.Combine(temp.FullName, "backups"),
                AutoBackupKeyPath = Path.Combine(temp.FullName, "autobackup.key"),
                DataDirectory = temp.FullName,
                TempDirectory = Path.Combine(temp.FullName, "tmp"),
            };
            Directory.CreateDirectory(options.AttachmentsDirectory);
            Directory.CreateDirectory(options.BackupsDirectory);

            var services = new ServiceCollection().AddRecordumServices(options).BuildServiceProvider();
            await services.GetRequiredService<ApplicationInitializer>().InitializeAsync();

            // Snapshot pre-backup row counts
            int habitsBefore, settingsBefore;
            using (var scope1 = services.CreateScope())
            await using (var db = await scope1.ServiceProvider.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync())
            {
                habitsBefore = await db.HabitNodes.IgnoreQueryFilters().CountAsync();
                settingsBefore = await db.AppSettings.CountAsync();
            }

            // Create backup
            string dest;
            using (var scope2 = services.CreateScope())
            {
                var backup = scope2.ServiceProvider.GetRequiredService<IBackupService>();
                dest = Path.Combine(options.BackupsDirectory, "e2e.recbak");
                await backup.CreateAsync(dest, "passphrase-1234567890", new BackupOptions(BackupKind.Manual));
            }
            File.Exists(dest).Should().BeTrue();

            // Mutate DB (delete a row if any exist, otherwise just verify backup exists)
            int habitCountAfterMutation;
            using (var scope3 = services.CreateScope())
            await using (var db = await scope3.ServiceProvider.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync())
            {
                if (await db.HabitNodes.IgnoreQueryFilters().AnyAsync())
                {
                    await db.HabitNodes.IgnoreQueryFilters().Take(1).ExecuteDeleteAsync();
                }
                habitCountAfterMutation = await db.HabitNodes.IgnoreQueryFilters().CountAsync();
            }

            // Restore
            using (var scope4 = services.CreateScope())
            {
                var restore = scope4.ServiceProvider.GetRequiredService<IRestoreService>();
                var result = await restore.RestoreAsync(dest, "passphrase-1234567890", new RestoreOptions(SkipSafetySnapshot: false));
                result.UnavailablePlugins.Should().BeEmpty();
            }

            // Verify counts restored
            using (var scope5 = services.CreateScope())
            await using (var db = await scope5.ServiceProvider.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync())
            {
                (await db.HabitNodes.IgnoreQueryFilters().CountAsync()).Should().Be(habitsBefore);
                (await db.AppSettings.CountAsync()).Should().Be(settingsBefore);
            }

            // Safety snapshot was created
            Directory.EnumerateFiles(options.BackupsDirectory, "pre-restore-*.recbak").Should().NotBeEmpty();

            await services.DisposeAsync();
        }
        finally
        {
            try { Directory.Delete(temp.FullName, recursive: true); } catch { }
        }
    }
}
