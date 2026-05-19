using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitTreeQueryServiceTests
{
    [Fact]
    public async Task Empty_db_returns_empty_list()
    {
        await using var fx = new DbFixture();
        var q = new HabitTreeQueryService(fx.Factory, fx.Clock);
        Assert.Empty(await q.GetHomeTreeAsync());
    }

    [Fact]
    public async Task Soft_deleted_nodes_are_excluded()
    {
        await using var fx = new DbFixture();
        var alive = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "alive" };
        var dead = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "dead", DeletedAt = DateTime.UtcNow };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.AddRange(alive, dead);
            await db.SaveChangesAsync();
        }
        var q = new HabitTreeQueryService(fx.Factory, fx.Clock);
        var rows = await q.GetHomeTreeAsync();
        Assert.Single(rows);
        Assert.Equal(alive.Id, rows[0].HabitNodeId);
    }

    [Fact]
    public async Task IsCompletedToday_true_when_completion_falls_in_today_window()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        var completion = new Completion { Id = Guid.NewGuid(), HabitNodeId = habit.Id, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = clock.UtcNowMs };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            db.Completions.Add(completion);
            await db.SaveChangesAsync();
        }
        var q = new HabitTreeQueryService(fx.Factory, clock);
        var rows = await q.GetHomeTreeAsync();
        Assert.True(rows.Single().IsCompletedToday);
    }
}
