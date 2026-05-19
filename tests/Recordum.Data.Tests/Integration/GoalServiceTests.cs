using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class GoalServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    [Fact]
    public async Task Create_then_list_returns_goal()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance);
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        var list = await svc.ListByHabitAsync(hid);
        Assert.Single(list);
        Assert.Equal(id, list[0].Id);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Close_sets_IsClosed_and_ClosedAt()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance);
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        await svc.CloseAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var g = await db.Goals.FindAsync(id);
        Assert.True(g!.IsClosed);
        Assert.NotNull(g.ClosedAt);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Delete_removes_goal()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new GoalService(fx.Factory, fx.Clock, NullLogger<GoalService>.Instance);
        var id = await svc.CreateAsync(new Goal { HabitNodeId = hid, Name = "g", TargetValue = 100, PeriodType = GoalPeriodType.Open });
        await svc.DeleteAsync(id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.False(await db.Goals.AnyAsync(x => x.Id == id));
        await fx.DisposeAsync();
    }
}
