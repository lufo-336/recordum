using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Timer;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Timer;

public class ActiveTimerRowViewModelTests
{
    [Fact]
    public void ElapsedDisplay_for_running_timer_is_wall_minus_started_minus_accumulated_pause()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(70_000));
        var t = new ActiveTimer
        {
            HabitNodeId = Guid.NewGuid(),
            StartedAtMs = 10_000,
            AccumulatedPausedMs = 5_000,
            TimerMode = TimerMode.Stopwatch,
        };
        var sut = new ActiveTimerRowViewModel(t, "habit", new FakeActiveTimerService(), clock);

        sut.Tick();

        sut.ElapsedSeconds.Should().Be(55);
        sut.ElapsedDisplay.Should().Be("00:55");
    }

    [Fact]
    public void ElapsedDisplay_switches_to_hms_after_one_hour()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(3_600_000));
        var t = new ActiveTimer { HabitNodeId = Guid.NewGuid(), StartedAtMs = 0, TimerMode = TimerMode.Stopwatch };
        var sut = new ActiveTimerRowViewModel(t, "habit", new FakeActiveTimerService(), clock);

        sut.Tick();

        sut.ElapsedSeconds.Should().Be(3600);
        sut.ElapsedDisplay.Should().Be("01:00:00");
    }

    [Fact]
    public void ElapsedDisplay_for_paused_timer_freezes_at_paused_minus_started_minus_accumulated()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(70_000));
        var t = new ActiveTimer
        {
            HabitNodeId = Guid.NewGuid(),
            StartedAtMs = 10_000,
            PausedAtMs = 40_000,
            AccumulatedPausedMs = 0,
            TimerMode = TimerMode.Stopwatch,
        };
        var sut = new ActiveTimerRowViewModel(t, "habit", new FakeActiveTimerService(), clock);

        sut.Tick();

        sut.ElapsedSeconds.Should().Be(30);
        sut.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void ElapsedSeconds_never_negative_when_clock_drifts_before_start()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(0));
        var t = new ActiveTimer { HabitNodeId = Guid.NewGuid(), StartedAtMs = 10_000, TimerMode = TimerMode.Stopwatch };
        var sut = new ActiveTimerRowViewModel(t, "habit", new FakeActiveTimerService(), clock);

        sut.Tick();

        sut.ElapsedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task PauseCommand_calls_PauseAsync_then_updates_local_state()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(70_000));
        var ats = new FakeActiveTimerService();
        var t = new ActiveTimer { HabitNodeId = Guid.NewGuid(), StartedAtMs = 10_000, TimerMode = TimerMode.Stopwatch };
        ats.Active.Add(t);
        var sut = new ActiveTimerRowViewModel(t, "habit", ats, clock);

        await sut.PauseCommand.ExecuteAsync(null);

        ats.PauseCalls.Should().ContainSingle().Which.Should().Be(t.HabitNodeId);
        sut.IsPaused.Should().BeTrue();
        t.PausedAtMs.Should().Be(70_000);
    }

    [Fact]
    public async Task ResumeCommand_accumulates_paused_duration_into_local_state()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(100_000));
        var ats = new FakeActiveTimerService();
        var t = new ActiveTimer
        {
            HabitNodeId = Guid.NewGuid(),
            StartedAtMs = 10_000,
            PausedAtMs = 70_000,
            AccumulatedPausedMs = 0,
            TimerMode = TimerMode.Stopwatch,
        };
        ats.Active.Add(t);
        var sut = new ActiveTimerRowViewModel(t, "habit", ats, clock) { IsPaused = true };

        await sut.ResumeCommand.ExecuteAsync(null);

        ats.ResumeCalls.Should().ContainSingle().Which.Should().Be(t.HabitNodeId);
        sut.IsPaused.Should().BeFalse();
        t.PausedAtMs.Should().BeNull();
        t.AccumulatedPausedMs.Should().Be(30_000);
    }

    [Fact]
    public async Task StopCommand_calls_StopAsync()
    {
        var clock = new FakeClock();
        var ats = new FakeActiveTimerService();
        var t = new ActiveTimer { HabitNodeId = Guid.NewGuid(), StartedAtMs = 0, TimerMode = TimerMode.Stopwatch };
        ats.Active.Add(t);
        var sut = new ActiveTimerRowViewModel(t, "habit", ats, clock);

        await sut.StopCommand.ExecuteAsync(null);

        ats.StopCalls.Should().ContainSingle().Which.Should().Be(t.HabitNodeId);
    }
}
