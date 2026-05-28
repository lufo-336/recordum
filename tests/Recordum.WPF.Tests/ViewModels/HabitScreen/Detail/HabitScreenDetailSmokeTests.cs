using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class HabitScreenDetailSmokeTests
{
    [Fact]
    public async Task Detail_loads_populated_from_seeded_completions()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"recordum-m37-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var collection = new ServiceCollection();
            collection.AddRecordumServices(new RecordumOptions
            {
                DatabasePath = Path.Combine(temp, "r.db"),
                KeyFilePath = Path.Combine(temp, "k"),
                SaltFilePath = Path.Combine(temp, "s"),
                AttachmentsDirectory = Path.Combine(temp, "attachments"),
                BackupsDirectory = Path.Combine(temp, "backups"),
                AutoBackupKeyPath = Path.Combine(temp, "autobackup.key"),
            });
            var sp = collection.BuildServiceProvider();
            try
            {
                await sp.GetRequiredService<ApplicationInitializer>().InitializeAsync();

                var nodes = sp.GetRequiredService<IHabitNodeService>();
                var configs = sp.GetRequiredService<IHabitConfigService>();
                var completions = sp.GetRequiredService<ICompletionService>();
                var habit = new HabitNode
                {
                    Id = Guid.NewGuid(),
                    Name = "smoke",
                    NodeType = NodeType.Habit,
                    HabitType = HabitType.Action,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                };
                await nodes.CreateAsync(habit);

                // M3.8 SchedulingAwareStreakAggregator requires a HabitConfig and uses
                // habit.CreatedAt as the schedule floor. The TimestampInterceptor overwrites
                // CreatedAt on insert, so we backdate it explicitly here for the smoke seed.
                await using (var db = await sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Recordum.Data.RecordumDbContext>>().CreateDbContextAsync())
                {
                    var tracked = await db.HabitNodes.FirstAsync(n => n.Id == habit.Id);
                    tracked.CreatedAt = DateTime.UtcNow.AddDays(-30);
                    await db.SaveChangesAsync();
                }
                await configs.UpsertAsync(habit.Id, c =>
                {
                    c.ScheduleType = ScheduleType.Daily;
                    c.StreakMode = StreakMode.Daily;
                    c.StreakRecovery = StreakRecovery.None;
                });
                for (int i = 0; i < 30; i++)
                {
                    var when = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-i).Date.AddHours(9 + (i % 6)), DateTimeKind.Utc);
                    await completions.RecordAsync(new Completion
                    {
                        Id = Guid.NewGuid(),
                        HabitNodeId = habit.Id,
                        CompletedAt = when,
                        CompletedAtMs = new DateTimeOffset(when, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                        IsRetroactive = false,
                    });
                }

                var screen = sp.GetRequiredService<HabitScreenViewModel>();
                await screen.LoadAsync(habit.Id);

                Assert.Equal(371, screen.Detail.YearHeatmap.Cells.Count);
                Assert.True(screen.Detail.Trend.Series.Count > 0);
                Assert.True(screen.Detail.Hourly.Buckets.Sum() > 0);
                Assert.True(screen.Detail.StreakSummary.LongestLength > 0);

                screen.Dispose();
            }
            finally
            {
                sp.Dispose();
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public async Task Detail_with_scheduling_aware_aggregator_reports_streak_length()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"recordum-m38-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var collection = new ServiceCollection();
            collection.AddRecordumServices(new RecordumOptions
            {
                DatabasePath = Path.Combine(temp, "r.db"),
                KeyFilePath = Path.Combine(temp, "k"),
                SaltFilePath = Path.Combine(temp, "s"),
                AttachmentsDirectory = Path.Combine(temp, "attachments"),
                BackupsDirectory = Path.Combine(temp, "backups"),
                AutoBackupKeyPath = Path.Combine(temp, "autobackup.key"),
            });
            var sp = collection.BuildServiceProvider();
            try
            {
                await sp.GetRequiredService<ApplicationInitializer>().InitializeAsync();
                var nodes = sp.GetRequiredService<IHabitNodeService>();
                var configs = sp.GetRequiredService<IHabitConfigService>();
                var completions = sp.GetRequiredService<ICompletionService>();

                var habit = new HabitNode
                {
                    Id = Guid.NewGuid(),
                    Name = "smoke-m38",
                    NodeType = NodeType.Habit,
                    HabitType = HabitType.Action,
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                };
                await nodes.CreateAsync(habit);
                // Interceptor overwrites CreatedAt on Add; backdate it via direct EF update.
                await using (var db = await sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Recordum.Data.RecordumDbContext>>().CreateDbContextAsync())
                {
                    var n = await db.HabitNodes.FirstAsync(x => x.Id == habit.Id);
                    n.CreatedAt = DateTime.UtcNow.AddDays(-15);
                    await db.SaveChangesAsync();
                }
                await configs.UpsertAsync(habit.Id, c =>
                {
                    c.ScheduleType = ScheduleType.Daily;
                    c.StreakMode = StreakMode.Daily;
                    c.StreakRecovery = StreakRecovery.None;
                });
                // 10 consecutive daily completions ending today.
                for (var i = 0; i < 10; i++)
                {
                    var when = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-i).Date.AddHours(10), DateTimeKind.Utc);
                    await completions.RecordAsync(new Completion
                    {
                        Id = Guid.NewGuid(),
                        HabitNodeId = habit.Id,
                        CompletedAt = when,
                        CompletedAtMs = new DateTimeOffset(when, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                        IsRetroactive = false,
                    });
                }

                var screen = sp.GetRequiredService<HabitScreenViewModel>();
                await screen.LoadAsync(habit.Id);

                Assert.True(screen.Detail.StreakSummary.CurrentLength >= 1,
                    $"expected current streak >= 1, got {screen.Detail.StreakSummary.CurrentLength}");
                Assert.True(screen.Detail.StreakSummary.LongestLength >= 10,
                    $"expected longest streak >= 10, got {screen.Detail.StreakSummary.LongestLength}");

                screen.Dispose();
            }
            finally { sp.Dispose(); }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }
}
