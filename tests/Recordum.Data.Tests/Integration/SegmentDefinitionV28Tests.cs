using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class SegmentDefinitionV28Tests
{
    [Fact]
    public async Task DurationSeconds_round_trips_and_defaults_null()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        Guid timedId, manualId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" });
            var timed = new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "Work", DurationSeconds = 30, SortOrder = 0 };
            var manual = new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "Lap", DurationSeconds = null, SortOrder = 1 };
            db.Set<SegmentDefinition>().AddRange(timed, manual);
            await db.SaveChangesAsync();
            timedId = timed.Id; manualId = manual.Id;
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Set<SegmentDefinition>().FirstAsync(s => s.Id == timedId)).DurationSeconds.Should().Be(30);
            (await db.Set<SegmentDefinition>().FirstAsync(s => s.Id == manualId)).DurationSeconds.Should().BeNull();
        }
    }
}
