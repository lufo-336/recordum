using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class HabitConfigHistoryVersioningV28Tests
{
    private static async Task<(DbFixture fx, Guid habitId, Guid cfgId)> SeedAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" };
        db.HabitNodes.Add(h);
        var c = new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = h.Id, SessionRepeatCount = 1 };
        db.Set<HabitConfig>().Add(c);
        await db.SaveChangesAsync();
        return (fx, h.Id, c.Id);
    }

    [Fact]
    public async Task Changing_SessionRepeatCount_creates_history_snapshot()
    {
        var (fx, habitId, cfgId) = await SeedAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var c = await db.Set<HabitConfig>().FirstAsync(x => x.Id == cfgId);
            c.SessionRepeatCount = 8;
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Set<HabitConfigHistory>().CountAsync(x => x.HabitNodeId == habitId)).Should().Be(1);
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Changing_NoteMarkerColor_does_not_create_history_snapshot()
    {
        var (fx, habitId, cfgId) = await SeedAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var c = await db.Set<HabitConfig>().FirstAsync(x => x.Id == cfgId);
            c.NoteMarkerColor = "#e0b341";
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Set<HabitConfigHistory>().CountAsync(x => x.HabitNodeId == habitId)).Should().Be(0);
        }
        await fx.DisposeAsync();
    }
}
