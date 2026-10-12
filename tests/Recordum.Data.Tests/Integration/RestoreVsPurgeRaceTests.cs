using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class RestoreVsPurgeRaceTests
{
    [Fact]
    public async Task Concurrent_restore_and_purge_leave_consistent_state()
    {
        var clock = new FakeClock();
        await using var fx = new DbFixture(clock);
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            await db.SaveChangesAsync();
        }
        var hn = new HabitNodeService(fx.Factory, clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance, new RecordingEventBus());
        await hn.SoftDeleteAsync(habit.Id);
        clock.Advance(TimeSpan.FromDays(40));
        var purge = new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance);
        var t1 = Task.Run(() => hn.RestoreAsync(habit.Id).AsTask());
        var t2 = Task.Run(() => purge.PurgeExpiredAsync(TimeSpan.FromDays(30)).AsTask());
        try { await Task.WhenAll(t1, t2); }
#pragma warning disable CA1031
        catch { /* one may throw — race tolerance */ }
#pragma warning restore CA1031
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var rows = await db2.HabitNodes.IgnoreQueryFilters().ToListAsync();
        if (rows.Count == 1)
            Assert.Null(rows[0].DeletedAt);
        else
            Assert.Empty(rows);
    }
}
