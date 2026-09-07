using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        => new(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);

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

    [Fact]
    public async Task PauseAsync_sets_paused_at_ms_and_is_idempotent()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Stopwatch);

        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(10));
        await sut.PauseAsync(habitId);

        var pausedAt = fx.Clock.UtcNow.ToUnixTimeMilliseconds();
        (await sut.GetActiveAsync(habitId))!.PausedAtMs.Should().Be(pausedAt);

        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(5));
        await sut.PauseAsync(habitId);
        (await sut.GetActiveAsync(habitId))!.PausedAtMs.Should().Be(pausedAt);
    }

    [Fact]
    public async Task PauseAsync_no_op_when_no_active_timer()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        await sut.PauseAsync(habitId);

        (await sut.GetActiveAsync(habitId)).Should().BeNull();
    }

    [Fact]
    public async Task ResumeAsync_clears_paused_at_ms_and_accumulates_pause_duration()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Stopwatch);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(10));
        await sut.PauseAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(7));
        await sut.ResumeAsync(habitId);

        var t = await sut.GetActiveAsync(habitId);
        t!.PausedAtMs.Should().BeNull();
        t.AccumulatedPausedMs.Should().Be(7_000);
    }

    [Fact]
    public async Task ResumeAsync_no_op_when_not_paused()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Stopwatch);

        await sut.ResumeAsync(habitId);

        var t = await sut.GetActiveAsync(habitId);
        t!.PausedAtMs.Should().BeNull();
        t.AccumulatedPausedMs.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveAsync_returns_null_when_no_active_timer()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        (await sut.GetActiveAsync(habitId)).Should().BeNull();
    }

    [Fact]
    public async Task ListActiveAsync_returns_all_running_or_paused_timers()
    {
        await using var fx = new DbFixture();
        var h1 = await SeedHabit(fx);
        var h2 = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(h1, TimerMode.Stopwatch);
        await sut.StartAsync(h2, TimerMode.Stopwatch);

        var list = await sut.ListActiveAsync();
        list.Select(t => t.HabitNodeId).Should().BeEquivalentTo(new[] { h1, h2 });
    }

    [Fact]
    public async Task ListActiveAsync_returns_empty_when_no_timers()
    {
        await using var fx = new DbFixture();
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        (await sut.ListActiveAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StopAsync_running_timer_creates_completion_and_deletes_row()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx, HabitType.Action);
        var completions = NewCompletions(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, completions);
        await sut.StartAsync(habitId, TimerMode.Stopwatch);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(60));

        var c = await sut.StopAsync(habitId);

        c.HabitNodeId.Should().Be(habitId);
        c.DurationSeconds.Should().Be(60);
        (await sut.GetActiveAsync(habitId)).Should().BeNull();

        await using var db = await fx.Factory.CreateDbContextAsync();
        var persisted = await db.Completions.Where(x => x.HabitNodeId == habitId).SingleAsync();
        persisted.DurationSeconds.Should().Be(60);
    }

    [Fact]
    public async Task StopAsync_throws_when_no_active_timer()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx, HabitType.Action);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        var act = async () => await sut.StopAsync(habitId);
        await act.Should().ThrowAsync<ActiveTimerNotFoundException>();
    }

    [Fact]
    public async Task StopAsync_paused_timer_uses_paused_at_as_end_and_subtracts_accumulated_pause()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx, HabitType.Action);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Stopwatch);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(30));
        await sut.PauseAsync(habitId);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(100));

        var c = await sut.StopAsync(habitId);

        c.DurationSeconds.Should().Be(30);
    }

    [Fact]
    public async Task StopWithoutRecording_returns_elapsed_and_removes_timer_without_completion()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx, HabitType.Counter);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Stopwatch);
        ((FakeClock)fx.Clock).Advance(TimeSpan.FromSeconds(95));

        var elapsed = await sut.StopWithoutRecordingAsync(habitId);

        elapsed.Should().Be(95);
        (await sut.GetActiveAsync(habitId)).Should().BeNull();

        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.Completions.AnyAsync(x => x.HabitNodeId == habitId)).Should().BeFalse();
    }

    [Fact]
    public async Task StopWithoutRecording_throws_when_no_active_timer()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx, HabitType.Counter);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        var act = async () => await sut.StopWithoutRecordingAsync(habitId);
        await act.Should().ThrowAsync<ActiveTimerNotFoundException>();
    }

    [Fact]
    public async Task NextSegmentAsync_increments_segment_index_for_session_mode()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Session);

        await sut.NextSegmentAsync(habitId);

        (await sut.GetActiveAsync(habitId))!.SegmentIndex.Should().Be(1);

        await sut.NextSegmentAsync(habitId);
        (await sut.GetActiveAsync(habitId))!.SegmentIndex.Should().Be(2);
    }

    [Fact]
    public async Task NextSegmentAsync_throws_when_not_in_session_mode()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));
        await sut.StartAsync(habitId, TimerMode.Stopwatch);

        var act = async () => await sut.NextSegmentAsync(habitId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task NextSegmentAsync_throws_when_no_active_timer()
    {
        await using var fx = new DbFixture();
        var habitId = await SeedHabit(fx);
        var sut = new ActiveTimerService(fx.Factory, fx.Clock, NewCompletions(fx));

        var act = async () => await sut.NextSegmentAsync(habitId);
        await act.Should().ThrowAsync<ActiveTimerNotFoundException>();
    }
}
