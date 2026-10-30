using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.Core.Services;
using Recordum.Core.Time;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.Presentation.Tests.Fixtures;

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
        var rig = BuildRig(settings, uiPrefs, new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero), null);
        notes = rig.Notes;
        configs = rig.Configs;
        return (rig.Vm, rig.Nodes);
    }

    /// <summary>Lo schermo con tutti i suoi finti a portata di mano: orologio e confine scelti dal test.</summary>
    public static HabitScreenRig CreateRig(DateTimeOffset now, DayBoundary? boundary = null, IGroupHistoryQueryService? groupHistory = null,
        QuickActionHandlerSelector? quickActions = null, ITickSourceFactory? ticks = null, IHabitStartQueryService? starts = null)
        => BuildRig(new FakeAppSettingService(), new FakeHabitUiPrefsService(), now, boundary, groupHistory, quickActions, ticks, starts);

    private static HabitScreenRig BuildRig(
        FakeAppSettingService settings, FakeHabitUiPrefsService uiPrefs, DateTimeOffset now, DayBoundary? boundary, IGroupHistoryQueryService? groupHistory = null,
        QuickActionHandlerSelector? quickActions = null, ITickSourceFactory? ticks = null, IHabitStartQueryService? starts = null)
    {
        var nodes = new FakeHabitNodeService();
        var completions = new FakeCompletionService();
        var timers = new FakeActiveTimerService();
        var metrics = new FakeMetricDefinitionService();
        var configService = new FakeHabitConfigService();
        var noteService = new FakeNoteService();
        var goals = new FakeGoalService();
        var abstinence = new FakeAbstinenceService();
        var clock = new FakeClock(now, boundary?.TimeZone);
        var bus = new FakeEventBus();
        var tree = new FakeHabitTreeQueryService();
        var editor = new HabitEditorViewModel(
            nodes, configService, goals, metrics, new FakeNotificationService(), new FakeTagService(),
            settings, new FakeReferenceData(), new RecentColorStore(settings), new FavoriteColorStore(settings),
            new FakeScreenColorPicker(), uiPrefs);

        var vm = new HabitScreenViewModel(
            nodes, completions, timers, metrics, new FakeReferenceData(), configService,
            noteService, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            abstinence, clock, bus, new FakeEditorAvailability(), settings, editor,
            tree, new FakeDiaryService(), new FakeConfirmationDialog(),
            new FakeAttachmentService(), new FakePhotoImporter(), new FakeTagService(), new FakeOpenFileDialog(), uiPrefs,
            new FakeResetTimeResolver(boundary), ticks, groupHistory, new FakePurgeService(), quickActions, starts);

        return new HabitScreenRig(vm, nodes, completions, noteService, configService, metrics, clock, abstinence, bus, tree, goals);
    }
}

public sealed record HabitScreenRig(
    HabitScreenViewModel Vm, FakeHabitNodeService Nodes, FakeCompletionService Completions, FakeNoteService Notes,
    FakeHabitConfigService Configs, FakeMetricDefinitionService Metrics, FakeClock Clock, FakeAbstinenceService Abstinence,
    FakeEventBus Bus, FakeHabitTreeQueryService Tree, FakeGoalService Goals);
