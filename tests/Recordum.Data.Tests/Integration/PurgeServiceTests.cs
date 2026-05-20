using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class PurgeServiceTests
{
    [Fact]
    public async Task Purge_deletes_subtree_in_child_to_parent_order()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit.Id, BodyMd = "n" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            db.Notes.Add(note);
            await db.SaveChangesAsync();
        }
        var hn = new HabitNodeService(fx.Factory, clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        await hn.SoftDeleteAsync(habit.Id);
        clock.Advance(TimeSpan.FromDays(31));
        var purge = new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance);
        var report = await purge.PurgeExpiredAsync(TimeSpan.FromDays(30));
        Assert.Equal(1, report.Purged);
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Empty(await db2.HabitNodes.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db2.Notes.ToListAsync());
    }

    [Fact]
    public async Task Purge_skips_recent_soft_deletes()
    {
        var clock = new FakeClock();
        await using var fx = new DbFixture(clock);
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Group, Name = "g" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            await db.SaveChangesAsync();
        }
        var hn = new HabitNodeService(fx.Factory, clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        await hn.SoftDeleteAsync(habit.Id);
        clock.Advance(TimeSpan.FromDays(5));
        var purge = new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance);
        var report = await purge.PurgeExpiredAsync(TimeSpan.FromDays(30));
        Assert.Equal(0, report.Purged);
    }

    [Fact]
    public async Task PurgeSubtreeAsync_hard_deletes_node_and_children()
    {
        var clock = new FakeClock();
        await using var fx = new DbFixture(clock);
        var parent = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Group, Name = "parent" };
        var child = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "child", ParentId = parent.Id };
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = child.Id, BodyMd = "n" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(parent);
            db.HabitNodes.Add(child);
            db.Notes.Add(note);
            await db.SaveChangesAsync();
        }

        var purge = new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance);
        await purge.PurgeSubtreeAsync(parent.Id);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Empty(await db2.HabitNodes.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db2.Notes.ToListAsync());
    }

    [Fact]
    public async Task PurgeSubtreeAsync_is_noop_for_nonexistent_id()
    {
        var clock = new FakeClock();
        await using var fx = new DbFixture(clock);
        var purge = new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance);

        // Should not throw.
        await purge.PurgeSubtreeAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task PurgeSubtreeAsync_also_removes_chart_series_references()
    {
        var clock = new FakeClock();
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "h", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.Charts.Add(new Chart { Id = chartId, Name = "c" });
            db.ChartSeries.Add(new ChartSeries { Id = Guid.NewGuid(), ChartId = chartId, HabitNodeId = habitId });
            await db.SaveChangesAsync();
        }

        var purge = new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance);
        await purge.PurgeSubtreeAsync(habitId);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.HabitNodes.IgnoreQueryFilters().AnyAsync(n => n.Id == habitId)).Should().BeFalse();
        (await db2.ChartSeries.AnyAsync(s => s.HabitNodeId == habitId)).Should().BeFalse();
    }
}
