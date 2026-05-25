using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
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

    [Fact]
    public async Task ListSegmentsAsync_returns_segments_in_sort_order()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        await svc.AddSegmentAsync(habitId, new SegmentDefinition { Name = "Rest", SortOrder = 1, IsRest = true });
        await svc.AddSegmentAsync(habitId, new SegmentDefinition { Name = "Work", SortOrder = 0, IsRest = false });

        var segments = await svc.ListSegmentsAsync(habitId);

        segments.Select(s => s.Name).Should().Equal("Work", "Rest");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateSegmentAsync_mutates_existing_row()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var seg = new SegmentDefinition { Id = Guid.NewGuid(), Name = "Work", SortOrder = 0 };
        await svc.AddSegmentAsync(habitId, seg);

        await svc.UpdateSegmentAsync(seg.Id, s => { s.Name = "Focus"; s.Color = "#abc123"; });

        await using var db = await fx.Factory.CreateDbContextAsync();
        var updated = await db.SegmentDefinitions.FindAsync(seg.Id);
        updated!.Name.Should().Be("Focus");
        updated.Color.Should().Be("#abc123");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListHistoryAsync_returns_snapshots_in_chronological_order()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var clock = (FakeClock)fx.Clock;
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);

        clock.Advance(TimeSpan.FromMinutes(10));
        await svc.UpsertAsync(habitId, c => c.StreakMode = StreakMode.Daily);
        clock.Advance(TimeSpan.FromMinutes(10));
        await svc.UpsertAsync(habitId, c => c.StreakMode = StreakMode.Weekly);
        clock.Advance(TimeSpan.FromMinutes(10));
        await svc.UpsertAsync(habitId, c => c.StreakMode = StreakMode.Occurrences);

        var history = await svc.ListHistoryAsync(habitId);

        history.Should().HaveCount(2);
        history[0].ValidFrom.Should().BeBefore(history[1].ValidFrom);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateSegmentAsync_throws_when_segment_missing()
    {
        await using var fx = new DbFixture();
        var svc = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var act = async () => await svc.UpdateSegmentAsync(Guid.NewGuid(), _ => { });
        await act.Should().ThrowAsync<SegmentDefinitionNotFoundException>();
    }
}
