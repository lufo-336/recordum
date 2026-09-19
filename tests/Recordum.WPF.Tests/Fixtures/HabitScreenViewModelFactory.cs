using Microsoft.Web.WebView2.Core;
using Recordum.Core.Services;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.WPF.Tests.Fixtures;

public static class HabitScreenViewModelFactory
{

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create()
        => Build(out _, out _, new FakeAppSettingService(), new FakeHabitUiPrefsService());

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create(out FakeNoteService notes)
        => Build(out notes, out _, new FakeAppSettingService(), new FakeHabitUiPrefsService());

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Create(out FakeNoteService notes, out FakeHabitConfigService configs)
        => Build(out notes, out configs, new FakeAppSettingService(), new FakeHabitUiPrefsService());

    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) CreateWithSettings(out FakeAppSettingService settings)
    {
        settings = new FakeAppSettingService();
        return Build(out _, out _, settings, new FakeHabitUiPrefsService());
    }

    /// <summary>Exposes both persistence stores so per-habit and legacy-global paths can be exercised.</summary>
    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) CreateWithStores(
        out FakeAppSettingService settings, out FakeHabitUiPrefsService uiPrefs)
    {
        settings = new FakeAppSettingService();
        uiPrefs = new FakeHabitUiPrefsService();
        return Build(out _, out _, settings, uiPrefs);
    }

    /// <summary>Builds over a caller-supplied settings store (open-columns persistence round-trips).</summary>
    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) CreateOver(FakeAppSettingService settings)
        => Build(out _, out _, settings, new FakeHabitUiPrefsService());

    /// <summary>Builds over caller-supplied stores (per-habit prefs round-trip across VM instances).</summary>
    public static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) CreateOver(
        FakeAppSettingService settings, FakeHabitUiPrefsService uiPrefs)
        => Build(out _, out _, settings, uiPrefs);

    private static (HabitScreenViewModel Vm, FakeHabitNodeService Nodes) Build(
        out FakeNoteService notes, out FakeHabitConfigService configs,
        FakeAppSettingService settings, FakeHabitUiPrefsService uiPrefs)
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
        var editor = new HabitEditorViewModel(
            nodes, configService, goals, metrics, new FakeNotificationService(), new FakeTagService(),
            settings, new FakeReferenceData(), new RecentColorStore(settings), new FavoriteColorStore(settings),
            new FakeScreenColorPicker(), uiPrefs);

        var vm = new HabitScreenViewModel(
            nodes, completions, timers, metrics, new FakeReferenceData(), configService,
            noteService, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            new FakeAbstinenceService(), clock, bus, new FakeWebView2Availability(), settings, editor,
            new FakeHabitTreeQueryService(), new FakeDiaryService(), new FakeConfirmationDialog(),
            new FakeAttachmentService(), new FakeOpenFileDialog(), uiPrefs, new FakeResetTimeResolver());

        notes = noteService;
        configs = configService;
        return (vm, nodes);
    }
}
