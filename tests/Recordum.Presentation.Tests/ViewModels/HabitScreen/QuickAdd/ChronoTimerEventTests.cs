using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.QuickAdd;

/// <summary>The per-cell stopwatch is an active_timer row: it can be stopped from elsewhere (the status bar).</summary>
public class ChronoTimerEventTests
{
    private static async Task<(CounterSeriesQuickAddViewModel Vm, FakeActiveTimerService Timers, FakeEventBus Bus, SeriesCellViewModel Cell, Guid Id)> OpenAsync()
    {
        var id = Guid.NewGuid();
        var metrics = new FakeMetricDefinitionService();
        var reps = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 };
        var tempo = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "Tempo", Type = MetricType.Seconds, SortOrder = 1 };
        metrics.Store[reps.Id] = reps;
        metrics.Store[tempo.Id] = tempo;
        var timers = new FakeActiveTimerService { StopWithoutRecordingFactory = _ => 95 };
        var bus = new FakeEventBus();
        var vm = new CounterSeriesQuickAddViewModel(new FakeCompletionService(), metrics, new FakeReferenceData(), new FakeClock(),
            timers, new FakeHabitConfigService(), null, bus)
        {
            Node = new HabitNode { Id = id, HabitType = HabitType.Counter },
        };
        await vm.LoadMetricsAsync();
        var cell = vm.Series[0].Cells.First(c => c.IsDuration);
        await vm.ToggleChronoCommand.ExecuteAsync(cell);
        vm.ChronoRunning.Should().BeTrue();
        return (vm, timers, bus, cell, id);
    }

    private static async Task Settled(Func<bool> condition)
    {
        for (var i = 0; i < 1000 && !condition(); i++) await Task.Delay(1);
    }

    [Fact]
    public async Task Stopping_the_cell_stopwatch_elsewhere_resets_the_cell()
    {
        var (vm, timers, bus, cell, id) = await OpenAsync();

        timers.Active.Clear();   // the status bar recorded and removed it
        bus.Publish(new TimerChangedPayload(id, WellKnownEvents.TimerStopped));
        await Settled(() => !vm.ChronoRunning);

        vm.ChronoRunning.Should().BeFalse();
        cell.IsChronoRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Pressing_the_cell_after_the_timer_vanished_does_not_throw_and_resets_it()
    {
        var (vm, timers, _, cell, id) = await OpenAsync();
        timers.Active.Clear();   // gone, and no event reached us
        timers.StopWithoutRecordingFactory = _ => throw new ActiveTimerNotFoundException(id);

        await vm.ToggleChronoCommand.ExecuteAsync(cell);

        vm.ChronoRunning.Should().BeFalse();
        cell.IsChronoRunning.Should().BeFalse();
    }

    [Fact]
    public async Task A_disposed_quick_add_ignores_timer_events()
    {
        var (vm, timers, bus, _, id) = await OpenAsync();
        vm.Dispose();
        var before = timers.GetActiveCalls;

        bus.Publish(new TimerChangedPayload(id, WellKnownEvents.TimerStopped));
        await Task.Delay(20);

        timers.GetActiveCalls.Should().Be(before);
        bus.HasSubscribers.Should().BeFalse();
    }
}
