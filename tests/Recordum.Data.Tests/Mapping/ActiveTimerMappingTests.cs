using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Mapping;

public class ActiveTimerMappingTests
{
    [Fact]
    public async Task ActiveTimer_round_trips_through_db()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "x" });
            db.ActiveTimers.Add(new ActiveTimer
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = 12345,
                TimerMode = TimerMode.Stopwatch,
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var row = await db2.ActiveTimers.SingleAsync();
        row.HabitNodeId.Should().Be(habitId);
        row.TimerMode.Should().Be(TimerMode.Stopwatch);
    }

    [Fact]
    public async Task ActiveTimer_unique_constraint_on_habit_node_id_rejects_duplicate()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "x" });
        db.ActiveTimers.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 1, TimerMode = TimerMode.Stopwatch });
        db.ActiveTimers.Add(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = habitId, StartedAtMs = 2, TimerMode = TimerMode.Stopwatch });

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
