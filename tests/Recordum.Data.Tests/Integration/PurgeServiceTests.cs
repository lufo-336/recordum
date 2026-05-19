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
}
