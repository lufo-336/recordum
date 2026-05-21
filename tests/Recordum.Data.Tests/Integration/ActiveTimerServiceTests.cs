using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ActiveTimerServiceTests
{
    private static CompletionService NewCompletions(DbFixture fx)
        => new(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());

    private static async Task<Guid> SeedHabit(DbFixture fx, HabitType type = HabitType.Counter)
    {
        var id = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "h" });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task StartAsync_inserts_row_with_now_and_clock()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        var t = await sut.StartAsync(habitId, TimerMode.Stopwatch);

        t.HabitNodeId.Should().Be(habitId);
        t.TimerMode.Should().Be(TimerMode.Stopwatch);
        t.StartedAtMs.Should().Be(fx.Clock.UtcNow.ToUnixTimeMilliseconds());
        t.PausedAtMs.Should().BeNull();
        t.AccumulatedPausedMs.Should().Be(0);
        t.SegmentIndex.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_initializes_segment_index_zero_for_session_mode()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        var t = await sut.StartAsync(habitId, TimerMode.Session);

        t.SegmentIndex.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_throws_when_a_timer_already_runs_for_that_habit()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        await sut.StartAsync(habitId, TimerMode.Stopwatch);

        var act = async () => await sut.StartAsync(habitId, TimerMode.Stopwatch);
        await act.Should().ThrowAsync<TimerAlreadyActiveException>();
    }
}
