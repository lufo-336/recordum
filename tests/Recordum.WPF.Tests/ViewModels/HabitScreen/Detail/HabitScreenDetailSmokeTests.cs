using System.IO;
using Microsoft.Data.Sqlite;
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
            });
            var sp = collection.BuildServiceProvider();
            try
            {
                await sp.GetRequiredService<ApplicationInitializer>().InitializeAsync();

                var nodes = sp.GetRequiredService<IHabitNodeService>();
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
}
