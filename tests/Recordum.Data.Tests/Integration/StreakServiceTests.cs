using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class StreakServiceTests
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
            Name = "streak-test",
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
    public async Task MarkBroken_sets_streak_broken_at()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var svc = new StreakService(configs, fx.Clock);

        await svc.MarkBrokenAsync(habitId);

        var cfg = await configs.GetAsync(habitId);
        cfg!.StreakBrokenAt.Should().NotBeNull();
        cfg.StreakBrokenAt!.Value.Should().Be(fx.Clock.UtcNow.UtcDateTime);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ClearBroken_nulls_field()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var svc = new StreakService(configs, fx.Clock);

        await svc.MarkBrokenAsync(habitId);
        await svc.ClearBrokenAsync(habitId);

        var cfg = await configs.GetAsync(habitId);
        cfg!.StreakBrokenAt.Should().BeNull();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task MarkBroken_does_not_snapshot_history()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var clock = (FakeClock)fx.Clock;
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var svc = new StreakService(configs, fx.Clock);

        // Initial UpsertAsync to anchor a baseline (no history yet for first write).
        clock.Advance(TimeSpan.FromMinutes(10));
        await configs.UpsertAsync(habitId, c => c.StreakMode = StreakMode.Daily);

        var historyBefore = (await configs.ListHistoryAsync(habitId)).Count;

        clock.Advance(TimeSpan.FromMinutes(10));
        await svc.MarkBrokenAsync(habitId);

        var historyAfter = (await configs.ListHistoryAsync(habitId)).Count;
        historyAfter.Should().Be(historyBefore);
        await fx.DisposeAsync();
    }
}
