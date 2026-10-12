using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class TimerControlViewModelTests
{
    private static TimerControlViewModel CreateSut(out FakeActiveTimerService ats, out FakeHabitConfigService configs, out FakeClock clock)
    {
        ats = new FakeActiveTimerService();
        configs = new FakeHabitConfigService();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        return new TimerControlViewModel(ats, configs, clock);
    }

    [Fact]
    public async Task SetContextAsync_no_timer_in_config_marks_not_applicable()
    {
        var sut = CreateSut(out _, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };

        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = false });

        sut.IsApplicable.Should().BeFalse();
        sut.StartCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SetContextAsync_with_timer_marks_applicable_and_allows_start()
    {
        var sut = CreateSut(out _, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };

        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });

        sut.IsApplicable.Should().BeTrue();
        sut.StartCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task StartCommand_starts_timer_with_mode_from_config()
    {
        var sut = CreateSut(out var ats, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Countdown });

        await sut.StartCommand.ExecuteAsync(null);

        ats.StartCalls.Should().ContainSingle().Which.Should().Be((node.Id, TimerMode.Countdown));
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task StopCommand_stops_timer_and_resets_state()
    {
        var sut = CreateSut(out var ats, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);

        await sut.StopCommand.ExecuteAsync(null);

        ats.StopCalls.Should().ContainSingle().Which.Should().Be(node.Id);
        sut.IsActive.Should().BeFalse();
        sut.ElapsedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task PauseCommand_then_ResumeCommand_toggles_state()
    {
        var sut = CreateSut(out var ats, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);

        await sut.PauseCommand.ExecuteAsync(null);
        ats.PauseCalls.Should().ContainSingle();
        sut.IsPaused.Should().BeTrue();
        sut.ResumeCommand.CanExecute(null).Should().BeTrue();

        await sut.ResumeCommand.ExecuteAsync(null);
        ats.ResumeCalls.Should().ContainSingle();
        sut.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task SetContextAsync_session_mode_populates_Segments()
    {
        var sut = CreateSut(out _, out var configs, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Work", SortOrder = 0 });
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Rest", SortOrder = 1 });

        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Session });

        sut.Segments.Should().HaveCount(2);
        sut.Segments[0].Name.Should().Be("Work");
    }

    [Fact]
    public async Task NextSegmentCommand_advances_through_segments_then_stops()
    {
        var sut = CreateSut(out var ats, out var configs, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "A", SortOrder = 0 });
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "B", SortOrder = 1 });
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Session });
        await sut.StartCommand.ExecuteAsync(null);

        await sut.NextSegmentCommand.ExecuteAsync(null);
        sut.SegmentIndex.Should().Be(1);
        ats.NextSegmentCalls.Should().ContainSingle();

        // Last segment → NextSegment stops the timer.
        await sut.NextSegmentCommand.ExecuteAsync(null);
        sut.IsActive.Should().BeFalse();
        ats.StopCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task NextSegmentCommand_disabled_for_non_session_mode()
    {
        var sut = CreateSut(out _, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);

        sut.NextSegmentCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task A_timer_stopped_from_the_status_bar_deactivates_the_control_and_its_tick()
    {
        var ats = new FakeActiveTimerService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var bus = new FakeEventBus();
        var ticks = new Recordum.Core.Tests.Abstractions.FakeTickSourceFactory();
        var sut = new TimerControlViewModel(ats, new FakeHabitConfigService(), clock, ticks, bus);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);
        ticks.RunningCount.Should().Be(1);

        ats.Active.Clear();   // the bar stopped it
        bus.Publish(new Recordum.Plugins.Contracts.Events.TimerChangedPayload(node.Id, Recordum.Plugins.Contracts.Events.WellKnownEvents.TimerStopped));
        for (var i = 0; i < 1000 && sut.IsActive; i++) await Task.Delay(1);

        sut.IsActive.Should().BeFalse();
        ticks.RunningCount.Should().Be(0);
    }

    [Fact]
    public async Task A_disposed_control_ignores_timer_events_and_holds_no_tick()
    {
        var ats = new FakeActiveTimerService();
        var bus = new FakeEventBus();
        var ticks = new Recordum.Core.Tests.Abstractions.FakeTickSourceFactory();
        var sut = new TimerControlViewModel(ats, new FakeHabitConfigService(), new FakeClock(), ticks, bus);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);
        ticks.RunningCount.Should().Be(1);
        var before = ats.GetActiveCalls;

        sut.Dispose();
        bus.Publish(new Recordum.Plugins.Contracts.Events.TimerChangedPayload(node.Id, Recordum.Plugins.Contracts.Events.WellKnownEvents.TimerStopped));
        await Task.Delay(20);

        ats.GetActiveCalls.Should().Be(before);
        ticks.RunningCount.Should().Be(0);
        bus.HasSubscribers.Should().BeFalse();
    }

    [Fact]
    public async Task A_failing_resync_does_not_escape_as_an_unobserved_exception()
    {
        var ats = new ThrowingTimers();
        var bus = new FakeEventBus();
        var sut = new TimerControlViewModel(ats, new FakeHabitConfigService(), new FakeClock(), null, bus);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        ats.Throw = true;
        var unobserved = new List<Exception>();
        void Handler(object? o, UnobservedTaskExceptionEventArgs e) => unobserved.Add(e.Exception);
        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            bus.Publish(new Recordum.Plugins.Contracts.Events.TimerChangedPayload(node.Id, Recordum.Plugins.Contracts.Events.WellKnownEvents.TimerStarted));
            await Task.Delay(20);
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
        finally { TaskScheduler.UnobservedTaskException -= Handler; }

        unobserved.Should().BeEmpty();
        ats.ThrewCount.Should().Be(1, "the resync did run and fail, inside the helper's try/catch");
    }

    [Fact]
    public async Task A_resync_for_the_previous_habit_finishing_late_does_not_write_onto_the_new_one()
    {
        var timers = new GatedTimers();
        var bus = new FakeEventBus();
        var sut = new TimerControlViewModel(timers, new FakeHabitConfigService(), new FakeClock(), null, bus);
        var cfg = (Guid id) => new HabitConfig { HabitNodeId = id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch };
        var a = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        var b = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(a, cfg(a.Id));
        timers.GateFor = a.Id;

        bus.Publish(new Recordum.Plugins.Contracts.Events.TimerChangedPayload(a.Id, Recordum.Plugins.Contracts.Events.WellKnownEvents.TimerStarted));
        await sut.SetContextAsync(b, cfg(b.Id));   // la schermata passa a B mentre la rilettura di A e' in volo
        timers.Release.SetResult(new ActiveTimer { Id = Guid.NewGuid(), HabitNodeId = a.Id, TimerMode = TimerMode.Stopwatch });
        await Task.Delay(50);

        sut.IsActive.Should().BeFalse("B non ha un timer: lo stato di A non deve finirci sopra");
    }

    private sealed class GatedTimers : Recordum.Core.Abstractions.Services.IActiveTimerService
    {
        public Guid? GateFor { get; set; }
        public TaskCompletionSource<ActiveTimer?> Release { get; } = new();
        public async ValueTask<ActiveTimer?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default)
            => habitNodeId == GateFor ? await Release.Task : null;
        public ValueTask<ActiveTimer> StartAsync(Guid h, TimerMode m, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask PauseAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask ResumeAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Completion> StopAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<int> StopWithoutRecordingAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask NextSegmentAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<ActiveTimer>> ListActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingTimers : Recordum.Core.Abstractions.Services.IActiveTimerService
    {
        public bool Throw { get; set; }
        public int ThrewCount { get; private set; }
        public ValueTask<ActiveTimer?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default)
        {
            if (!Throw) return new((ActiveTimer?)null);
            ThrewCount++;
            throw new InvalidOperationException("db down");
        }
        public ValueTask<ActiveTimer> StartAsync(Guid h, TimerMode m, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask PauseAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask ResumeAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Completion> StopAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<int> StopWithoutRecordingAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask NextSegmentAsync(Guid h, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<ActiveTimer>> ListActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
