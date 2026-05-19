using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitConfigServiceTests
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
    public async Task UpsertAsync_writes_through_interceptor_creating_history_row()
    {
        var (fx, habitId) = await SeedHabitAsync();
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromMinutes(10));
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        await svc.UpsertAsync(habitId, c =>
        {
            c.ScheduleType = ScheduleType.WeeklyX;
            c.ScheduleWeeklyTarget = 3;
        });
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Single(db.HabitConfigHistories);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpsertAsync_rejects_inconsistent_schedule()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpsertAsync(habitId, c =>
            {
                c.ScheduleType = ScheduleType.WeeklyX;
                c.ScheduleWeeklyTarget = 0;
            }).AsTask());
        Assert.Contains("ScheduleWeeklyTarget", ex.Message, StringComparison.Ordinal);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task AddSegment_then_Remove_round_trip()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var seg = new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "work", SortOrder = 0 };
        await svc.AddSegmentAsync(habitId, seg);
        await using (var db = await fx.Factory.CreateDbContextAsync())
            Assert.Single(db.SegmentDefinitions);
        await svc.RemoveSegmentAsync(seg.Id);
        await using (var db = await fx.Factory.CreateDbContextAsync())
            Assert.Empty(db.SegmentDefinitions);
        await fx.DisposeAsync();
    }
}
