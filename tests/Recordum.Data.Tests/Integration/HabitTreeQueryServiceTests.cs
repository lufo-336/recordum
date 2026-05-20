using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitTreeQueryServiceTests
{
    private static readonly int[] ExpectedSevenDayBuckets = [1, 0, 0, 0, 0, 0, 2];

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

    [Fact]
    public async Task GetHomeTreeAsync_returns_PinScope_from_db()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                Name = "Pinned",
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                PinScope = PinScope.Global,
                Color = "#ff0000",
            });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == habitId).PinScope.Should().Be(PinScope.Global);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_ChildCount_for_groups()
    {
        await using var fx = new DbFixture();
        var groupId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = groupId, Name = "G", NodeType = NodeType.Group });
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), Name = "Child1", NodeType = NodeType.Habit, HabitType = HabitType.Action, ParentId = groupId });
            db.HabitNodes.Add(new HabitNode { Id = Guid.NewGuid(), Name = "Child2", NodeType = NodeType.Habit, HabitType = HabitType.Action, ParentId = groupId });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == groupId).ChildCount.Should().Be(2);
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_IsArchived_flag()
    {
        await using var fx = new DbFixture();
        var id = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = id, Name = "A", NodeType = NodeType.Habit, HabitType = HabitType.Action, IsArchived = true });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == id).IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetHomeTreeAsync_returns_7day_completion_counts_oldest_to_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        await using var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        var today = clock.UtcNow.UtcDateTime.Date;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, Name = "H", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.Completions.AddRange(
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today.AddDays(-6), CompletedAtMs = new DateTimeOffset(today.AddDays(-6), TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today, CompletedAtMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = today, CompletedAtMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds() + 1 });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, clock);
        var rows = await q.GetHomeTreeAsync();
        rows.Single(r => r.HabitNodeId == habitId).CompletionsLast7Days
            .Should().BeEquivalentTo(ExpectedSevenDayBuckets);
    }

    [Fact]
    public async Task GetTrashTreeAsync_returns_only_soft_deleted_rows_flat()
    {
        await using var fx = new DbFixture();
        var live = Guid.NewGuid();
        var trashed = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = live, Name = "Live", NodeType = NodeType.Habit, HabitType = HabitType.Action });
            db.HabitNodes.Add(new HabitNode { Id = trashed, Name = "Trashed", NodeType = NodeType.Habit, HabitType = HabitType.Action, DeletedAt = fx.Clock.UtcNow.UtcDateTime });
            await db.SaveChangesAsync();
        }

        var q = new HabitTreeQueryService(fx.Factory, fx.Clock);
        var rows = await q.GetTrashTreeAsync();
        rows.Should().ContainSingle().Which.HabitNodeId.Should().Be(trashed);
    }
}
