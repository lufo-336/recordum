using Microsoft.Web.WebView2.Core;
using Recordum.Core.Services;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.Tests.Fixtures;

public static class HabitScreenViewModelFactory
{

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create()
        => Build(out _, out _);

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create(out FakeNoteService notes)
        => Build(out notes, out _);

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create(out FakeNoteService notes, out FakeHabitConfigService configs)
        => Build(out notes, out configs);

    private static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Build(out FakeNoteService notes, out FakeHabitConfigService configs)
    {
        var nodes = new FakeHabitNodeService();
        var completions = new FakeCompletionService();
        var timers = new FakeActiveTimerService();
        var metrics = new FakeMetricDefinitionService();
        var configService = new FakeHabitConfigService();
        var noteService = new FakeNoteService();
        var goals = new FakeGoalService();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var bus = new FakeEventBus();

        var vm = new HabitScreenViewModel(
            nodes, completions, timers, metrics, new FakeReferenceData(), configService,
            noteService, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            clock, bus, new FakeWebView2Availability());

        notes = noteService;
        configs = configService;
        return (vm, nodes);
    }
}
