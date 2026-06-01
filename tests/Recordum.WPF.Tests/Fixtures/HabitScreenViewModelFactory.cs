using Microsoft.Web.WebView2.Core;
using Recordum.Core.Services;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.Tests.Fixtures;

public static class HabitScreenViewModelFactory
{
    private sealed class FakeWebView2Availability : IWebView2Availability
    {
        public bool IsAvailable => true;
        public CoreWebView2Environment? Environment => null;
        public Task ProbeAsync() => Task.CompletedTask;
    }

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create()
    {
        var result = Create(out _);
        return result;
    }

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create(out FakeNoteService notes)
    {
        var nodes = new FakeHabitNodeService();
        var completions = new FakeCompletionService();
        var timers = new FakeActiveTimerService();
        var metrics = new FakeMetricDefinitionService();
        var configs = new FakeHabitConfigService();
        var noteService = new FakeNoteService();
        var goals = new FakeGoalService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var bus = new FakeEventBus();

        var vm = new HabitScreenViewModel(
            nodes, completions, timers, metrics, configs,
            noteService, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            clock, bus, new FakeWebView2Availability());

        notes = noteService;
        return (vm, nodes);
    }
}
