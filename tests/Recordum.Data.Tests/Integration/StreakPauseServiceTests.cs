using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class StreakPauseServiceTests
{
    private static async Task<(DbFixture fx, Guid hid)> SeedAsync()
    {
        var fx = new DbFixture();
        var hid = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = hid, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" });
        await db.SaveChangesAsync();
        return (fx, hid);
    }

    [Fact]
    public async Task StartAsync_rejects_to_before_from()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.StartAsync(hid, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 1), null).AsTask());
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListByHabit_returns_ordered_by_PauseFrom()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        await svc.StartAsync(hid, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12), null);
        await svc.StartAsync(hid, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), null);
        var list = await svc.ListByHabitAsync(hid);
        Assert.Equal(2, list.Count);
        Assert.True(list[0].PauseFrom < list[1].PauseFrom);
        await fx.DisposeAsync();
    }
}
