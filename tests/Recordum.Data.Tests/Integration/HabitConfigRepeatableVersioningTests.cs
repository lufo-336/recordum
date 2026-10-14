using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

/// <summary>
/// Regression/characterization test proving that <see cref="HabitConfig.Repeatable"/> and
/// <see cref="HabitConfig.SeriesLabelsJson"/> are versioned into <see cref="HabitConfigHistory"/>
/// (Onda 2.0 Task 2). The wiring was added in Task 1
/// (<c>HabitConfigHistoryColumns.Versioned</c> already lists both columns), so this test is
/// expected to pass on first run.
/// </summary>
public class HabitConfigRepeatableVersioningTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedHabitAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var habit = new HabitNode
        {
            Id = Guid.NewGuid(),
            NodeType = NodeType.Habit,
            HabitType = HabitType.Counter,
            Name = "x",
        };
        var cfg = new HabitConfig
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habit.Id,
            ScheduleType = ScheduleType.Daily,
            StreakMode = StreakMode.Daily,
            StreakRecovery = StreakRecovery.None,
        };
        db.HabitNodes.Add(habit);
        db.HabitConfigs.Add(cfg);
        await db.SaveChangesAsync();
        return (fx, habit.Id);
    }

    [Fact]
    public async Task Changing_repeatable_writes_a_history_snapshot_of_the_previous_value()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());

        // Set repeatable to a NON-default value (true) so the captured old value is unambiguous:
        // bool defaults to false, so seeding/asserting false could be satisfied by an unrelated
        // history row and would pass even if "Repeatable" were dropped from the Versioned list.
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromMinutes(10));
        await svc.UpsertAsync(habitId, c =>
        {
            c.Repeatable = true;
            c.SeriesLabelsJson = "[\"A\"]";
        });

        // Flip repeatable back off — this is the change under test.
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromMinutes(10));
        await svc.UpsertAsync(habitId, c => c.Repeatable = false);

        // ListHistoryAsync orders by ValidFrom ascending, so the LAST row is the snapshot the flip
        // produced. It must capture the previous value (true) — this only holds if the interceptor
        // treats Repeatable as a versioned column.
        var history = await svc.ListHistoryAsync(habitId);
        Assert.Equal(2, history.Count);
        Assert.True(history[^1].Repeatable);

        var current = await svc.GetAsync(habitId);
        Assert.False(current!.Repeatable);
        Assert.Equal("[\"A\"]", current.SeriesLabelsJson);

        await fx.DisposeAsync();
    }
}
