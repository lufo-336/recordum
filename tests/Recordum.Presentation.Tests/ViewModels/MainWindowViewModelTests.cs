using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Tests.Abstractions;
using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Core.Time;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Charts;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.Presentation.ViewModels.Timer;
using Recordum.Presentation.ViewModels.Workspace;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task Initialize_calls_HomeViewModel_LoadCommand()
    {
        var (sut, parts) = CreateSut();

        await sut.InitializeAsync();

        sut.Home.State.Should().NotBe(HomeState.Loading);
        parts.Query.HomeCallCount.Should().Be(1);
    }

    [Fact]
    public void Home_and_Workspace_are_exposed_to_the_view()
    {
        var (sut, parts) = CreateSut();

        sut.Home.Should().BeSameAs(parts.Home);
        sut.Workspace.Should().BeSameAs(parts.Workspace);
    }

    [Fact]
    public async Task BackupSettings_opens_settings_window_at_backup_category()
    {
        var (sut, parts) = CreateSut();
        await sut.BackupSettingsCommand.ExecuteAsync(null);
        parts.SettingsLauncher.ShowCalls.Should().Equal(SettingsCategoryId.Backup);
    }

    [Fact]
    public async Task ShowDiagnosticInfo_opens_settings_window_at_diagnostics_category()
    {
        var (sut, parts) = CreateSut();
        await sut.ShowDiagnosticInfoCommand.ExecuteAsync(null);
        parts.SettingsLauncher.ShowCalls.Should().Equal(SettingsCategoryId.Diagnostics);
    }

    [Fact]
    public async Task ShowSettings_opens_settings_window_at_default_category()
    {
        var (sut, parts) = CreateSut();
        await sut.ShowSettingsCommand.ExecuteAsync(null);
        parts.SettingsLauncher.ShowCalls.Should().Equal((SettingsCategoryId?)null);
    }

    [Fact]
    public async Task Selecting_a_habit_row_opens_it_as_a_preview_tab()
    {
        var (sut, parts) = CreateSut();
        var habitId = Guid.NewGuid();
        parts.Nodes.Store[habitId] = new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Corsa", Color = "#000000" };
        parts.Query.HomeRows.Add(SampleRow(habitId, "Corsa"));
        await sut.InitializeAsync();

        sut.Home.SelectedRow = sut.Home.Rows.First(r => r.HabitNodeId == habitId);
        await Task.Delay(30);

        sut.Workspace.Tabs.Should().ContainSingle();
        sut.Workspace.Tabs[0].SubjectId.Should().Be(habitId);
        sut.Workspace.Tabs[0].IsPreview.Should().BeTrue();
        sut.Workspace.Tabs[0].Title.Should().Be("Corsa");
    }

    [Fact]
    public void ShowHome_on_the_home_workspace_toggles_the_sidebar()
    {
        // Model A: ⌂ on Home collapses/expands the tree sidebar so an open habit's columns + focus can
        // fill the window. (Making the tree fill the window instead is via empty-click / closing tabs.)
        var (sut, _) = CreateSut();
        sut.IsHomeWorkspace.Should().BeTrue();
        sut.Workspace.SidebarCollapsed.Should().BeFalse();

        sut.ShowHomeCommand.Execute(null);
        sut.Workspace.SidebarCollapsed.Should().BeTrue();

        sut.ShowHomeCommand.Execute(null);
        sut.Workspace.SidebarCollapsed.Should().BeFalse();
    }

    [Fact]
    public async Task Tree_background_click_returns_to_full_home_even_with_no_row_selected()
    {
        // The bug: a tab re-activated from the strip leaves SelectedRow null, so setting it null again
        // wouldn't notify. ClickBackground must still return to the full-width Home.
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "yoga", Color = "#000000" };
        parts.Query.HomeRows.Add(SampleRow(id, "yoga"));
        await sut.InitializeAsync();
        sut.Home.SelectRowCommand.Execute(sut.Home.Rows.First());
        await Task.Delay(30);
        sut.Workspace.PinActiveTab();
        sut.Home.SelectRowCommand.Execute(null);   // deselect → Panoramica, tab stays pinned
        await Task.Delay(30);
        sut.Workspace.ActivateTab(sut.Workspace.Tabs[0]);   // re-activate from the strip; SelectedRow stays null
        sut.Workspace.IsPanoramicaActive.Should().BeFalse();
        sut.Home.SelectedRow.Should().BeNull();

        sut.Home.ClickBackground();

        sut.Workspace.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task Clearing_selection_activates_panoramica()
    {
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "yoga", Color = "#000000" };
        parts.Query.HomeRows.Add(new HomeTreeRow(
            id, null, "yoga", "#000000", HabitType.Action, null, 0, false,
            PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty));
        await sut.InitializeAsync();
        sut.Home.SelectRowCommand.Execute(sut.Home.Rows.First());
        await Task.Delay(30);
        sut.Workspace.Tabs.Should().ContainSingle();

        sut.Home.SelectRowCommand.Execute(null);
        await Task.Delay(30);

        sut.Workspace.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task Default_workspace_is_Home_and_can_switch_to_Charts()
    {
        var (sut, _) = CreateSut();
        Assert.Equal(AppWorkspace.Home, sut.CurrentWorkspace);

        await sut.ShowChartsCommand.ExecuteAsync(null);

        Assert.Equal(AppWorkspace.Charts, sut.CurrentWorkspace);
        Assert.True(sut.IsChartsWorkspace);
        Assert.False(sut.IsHomeWorkspace);
    }

    [Fact]
    public async Task ExportLibrary_invokes_the_launcher()
    {
        var (sut, parts) = CreateSut();
        await sut.ExportLibraryCommand.ExecuteAsync(null);
        Assert.Equal(1, parts.ExportLauncher.LibraryCount);
    }

    [Fact]
    public void ShowHome_switches_back()
    {
        var (sut, _) = CreateSut();
        sut.ShowChartsCommand.Execute(null);
        sut.ShowHomeCommand.Execute(null);
        Assert.Equal(AppWorkspace.Home, sut.CurrentWorkspace);
    }

    [Fact]
    public async Task Selecting_same_subject_twice_reuses_the_preview_tab()
    {
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", Color = "#808080" };
        parts.Query.HomeRows.Add(SampleRow(id, "a"));
        await sut.InitializeAsync();

        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == id);
        await Task.Delay(30);
        sut.Home.SelectedRow = null;
        await Task.Delay(30);
        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == id);
        await Task.Delay(30);

        // Re-opening an already-open preview just reactivates it; only one tab exists.
        sut.Workspace.Tabs.Should().ContainSingle();
        sut.Workspace.Tabs[0].SubjectId.Should().Be(id);
    }

    [Fact]
    public async Task Switching_subjects_replaces_the_preview_tab()
    {
        var (sut, parts) = CreateSut();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        parts.Nodes.Store[a] = new HabitNode { Id = a, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", Color = "#808080" };
        parts.Nodes.Store[b] = new HabitNode { Id = b, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "b", Color = "#808080" };
        parts.Query.HomeRows.Add(SampleRow(a, "a"));
        parts.Query.HomeRows.Add(SampleRow(b, "b"));
        await sut.InitializeAsync();

        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == a);
        await Task.Delay(30);
        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == b);
        await Task.Delay(30);

        // Second preview replaces the first: only one tab total.
        sut.Workspace.Tabs.Should().ContainSingle();
        sut.Workspace.Tabs[0].SubjectId.Should().Be(b);
        sut.Workspace.Tabs[0].IsPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Closing_all_tabs_persists_workspace_state()
    {
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Corsa", Color = "#000000" };
        parts.Query.HomeRows.Add(SampleRow(id, "Corsa"));
        await sut.InitializeAsync();
        sut.Home.SelectedRow = sut.Home.Rows.First(r => r.HabitNodeId == id);
        await Task.Delay(30);
        sut.Workspace.PinActiveTab();

        sut.Workspace.CloseTab(sut.Workspace.Tabs[0]);
        await Task.Delay(30);

        var raw = await parts.AppSettings.GetAsync(WorkspaceViewModel.StateSettingKey);
        raw.Should().NotBeNull(); // save was triggered on the change
    }

    // ---- Onda "Giornata", Task 8: dalla Home la schermata si apre sul giorno scelto ----
    // Martedì 10 novembre 2026 alle 10:00, app e abitudine a mezzanotte UTC: ieri è lunedì 9.

    private static readonly DateTimeOffset TuesdayMorning = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Tuesday = new(2026, 11, 10), Monday = new(2026, 11, 9), Saturday = new(2026, 11, 7);

    [Fact]
    public async Task Opening_a_habit_from_a_past_day_selects_that_day()
    {
        var d = await CreateDaySutAsync(HabitType.Action);
        d.Home.PreviousDayCommand.Execute(null);

        d.Home.SelectedRow = d.Row;
        await Task.Delay(30);

        d.Home.SelectedDay.Should().Be(Monday);
        d.Screen.SelectedDay.Should().Be(Monday);
        DateOnly.FromDateTime(d.Screen.QuickAdd!.CompletionDate).Should().Be(Monday, "ciò che si registra dalla schermata va su quel giorno");
    }

    [Fact]
    public async Task A_quick_action_that_opens_the_screen_from_a_past_day_selects_that_day()
    {
        var d = await CreateDaySutAsync(HabitType.Action);
        d.Home.PreviousDayCommand.Execute(null);

        // La riga non è selezionata: il log rapido risponde "non da qui" e la Home la seleziona.
        await d.Row.QuickActionCommand.ExecuteAsync(null);
        await Task.Delay(30);
        d.Handler.Days.Should().Equal(Monday);
        d.Screen.SelectedDay.Should().Be(Monday);

        // La riga è già selezionata (HabitActivationRequested): la schermata, rimessa su oggi, torna sul giorno della Home.
        await d.Screen.SelectDayAsync(Tuesday);
        await d.Row.QuickActionCommand.ExecuteAsync(null);
        await Task.Delay(30);
        d.Screen.SelectedDay.Should().Be(Monday);
    }

    [Fact]
    public async Task Opening_from_today_leaves_the_screen_on_its_day()
    {
        // Da oggi la Home non tocca il giorno della schermata: un diario aperto sulla bozza di ieri non riceve la
        // domanda "cambiare giorno?" a ogni apertura.
        var d = await CreateDaySutAsync(HabitType.Action);
        d.Home.SelectedRow = d.Row;
        await Task.Delay(30);
        d.Screen.SelectedDay.Should().Be(Tuesday);
        await d.Screen.SelectDayAsync(Saturday);

        d.Home.SelectedRow = null;
        d.Home.SelectedRow = d.Row;
        await Task.Delay(30);
        d.Screen.SelectedDay.Should().Be(Saturday);

        await d.Row.QuickActionCommand.ExecuteAsync(null);
        await Task.Delay(30);
        d.Screen.SelectedDay.Should().Be(Saturday);
    }

    [Fact]
    public async Task Rebuilding_the_Home_does_not_move_an_open_screen()
    {
        // La Home rifà le righe a ogni registrazione e a ogni cambio di giorno, e ri-seleziona la stessa abitudine:
        // non è un'apertura. Il giorno che l'utente ha scelto nella schermata resta.
        var d = await CreateDaySutAsync(HabitType.Action);
        d.Home.PreviousDayCommand.Execute(null);
        d.Home.SelectedRow = d.Row;
        await Task.Delay(30);
        d.Screen.SelectedDay.Should().Be(Monday);
        await d.Screen.SelectDayAsync(Saturday);

        d.Home.PreviousDayCommand.Execute(null);   // domenica 8: righe rifatte, selezione ripristinata
        await Task.Delay(30);
        d.Home.SelectedRow.Should().NotBeNull();
        d.Screen.SelectedDay.Should().Be(Saturday);

        await d.Home.LoadCommand.ExecuteAsync(null);
        d.Home.SelectInteractive(d.Row, ctrl: false, shift: false);   // stessa abitudine, riga nuova
        await Task.Delay(30);
        d.Screen.SelectedDay.Should().Be(Saturday);
    }

    [Fact]
    public async Task Quick_action_on_an_abstinence_asks_for_the_relapse_prompt()
    {
        var d = await CreateDaySutAsync(HabitType.Resistance, abstaining: true);
        d.Home.PreviousDayCommand.Execute(null);

        await d.Row.QuickActionCommand.ExecuteAsync(null);
        await Task.Delay(30);

        d.Screen.SelectedDay.Should().Be(Monday);
        var ab = (AbstinenceQuickAddViewModel)d.Screen.QuickAdd!;
        DateOnly.FromDateTime(ab.CompletionDate).Should().Be(Monday, "la ricaduta proposta porta quella data");
        // La vista non c'era ancora quando la domanda è partita: la trova ad aspettarla, una volta sola.
        ab.TakeLapsePrompt().Should().BeTrue();
        ab.TakeLapsePrompt().Should().BeFalse();

        // Con la schermata già aperta (riga già selezionata) l'evento si alza, una volta.
        var raised = 0;
        ab.LapsePromptRequested += (_, _) => raised++;
        await d.Row.QuickActionCommand.ExecuteAsync(null);
        await Task.Delay(30);
        raised.Should().Be(1);
    }

    [Fact]
    public async Task Quick_action_on_an_abstinence_today_asks_for_the_relapse_prompt_on_today()
    {
        var d = await CreateDaySutAsync(HabitType.Resistance, abstaining: true);

        await d.Row.QuickActionCommand.ExecuteAsync(null);
        await Task.Delay(30);

        d.Screen.SelectedDay.Should().Be(Tuesday);
        ((AbstinenceQuickAddViewModel)d.Screen.QuickAdd!).TakeLapsePrompt().Should().BeTrue();
    }

    [Fact]
    public async Task Selecting_an_abstinence_row_does_not_ask_for_the_relapse_prompt()
    {
        var d = await CreateDaySutAsync(HabitType.Resistance, abstaining: true);
        d.Home.PreviousDayCommand.Execute(null);

        d.Home.SelectedRow = d.Row;
        await Task.Delay(30);

        d.Screen.SelectedDay.Should().Be(Monday);
        ((AbstinenceQuickAddViewModel)d.Screen.QuickAdd!).TakeLapsePrompt().Should().BeFalse("selezionare non è chiedere di registrare");
    }

    [Fact]
    public async Task Opening_a_group_from_a_past_day_leaves_it_on_today()
    {
        // La Panoramica di un gruppo resta su oggi (Task 6): la sua schermata non prende il giorno della Home.
        var d = await CreateDaySutAsync(type: null);
        d.Home.PreviousDayCommand.Execute(null);

        d.Home.SelectedRow = d.Row;
        await Task.Delay(30);

        d.Screen.SelectedDay.Should().Be(Tuesday);
    }

    private sealed record DaySut(MainWindowViewModel Sut, Guid Id, DecliningHandler Handler)
    {
        public HomeViewModel Home => Sut.Home;
        public HomeRowViewModel Row => Sut.Home.Rows.Single(r => r.HabitNodeId == Id);
        public HabitScreenViewModel Screen => Sut.Workspace.Tabs.Single(t => t.SubjectId == Id).Screen;
    }

    /// <summary>Una Home col suo orologio e un'abitudine sola, il cui log rapido risponde sempre "non da qui"; le
    /// schermate hanno lo stesso orologio e conoscono l'abitudine (in astinenza, se chiesto).</summary>
    private static async Task<DaySut> CreateDaySutAsync(HabitType? type, bool abstaining = false)
    {
        var id = Guid.NewGuid();
        var node = new HabitNode
        {
            Id = id, NodeType = type is null ? NodeType.Group : NodeType.Habit, HabitType = type, Name = "Soggetto", Color = "#2F7FC1",
        };
        var query = new FakeHabitTreeQueryService();
        query.HomeRows.Add(new HomeTreeRow(id, null, node.Name, node.Color, type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(new int[7]), ImmutableArray<Guid>.Empty, CompletionsLast14Days: ImmutableArray.Create(new int[14])));
        var handler = new DecliningHandler(type ?? HabitType.Action);
        var clock = new FakeClock(TuesdayMorning);
        var nodes = new FakeHabitNodeService();
        nodes.Store[id] = node;
        var home = new HomeViewModel(query, nodes, new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([handler]), clock: clock,
            resetTimes: new FakeResetTimeResolver(new DayBoundary(TimeZoneInfo.Utc, TimeOnly.MinValue)), ticks: new FakeTickSourceFactory());
        var settings = new FakeAppSettingService();
        var workspace = new WorkspaceViewModel(new RigScreenFactory(node, abstaining), settings);
        var charts = new ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(), new ExtensionPointRegistry(),
            new ChartBuilderViewModel(new FakeChartService(), new FakeHabitTreeQueryService(), new FakeMetricDefinitionService(),
                new FakeChartQueryService(), new ExtensionPointRegistry(), null));
        var sut = new MainWindowViewModel(home, settings,
            new TimerStatusBarViewModel(new FakeActiveTimerService(), clock, nodes, new FakeEventBus(), NullTickSourceFactory.Instance),
            workspace, charts, new FakeExportDialogLauncher(), new FakeBackupDialogLauncher(),
            new BackupHistoryViewModel(new FakeBackupLogService(), new FakeFolderOpener()), new FakeSettingsWindowLauncher(),
            new FakeApplicationLifetime());
        await sut.InitializeAsync();
        return new DaySut(sut, id, handler);
    }

    private sealed class RigScreenFactory(HabitNode node, bool abstaining) : ISubjectScreenFactory
    {
        public SubjectScreen Create()
        {
            var rig = HabitScreenViewModelFactory.CreateRig(TuesdayMorning);
            rig.Nodes.Store[node.Id] = node;
            if (abstaining)
                rig.Abstinence.States[node.Id] = new AbstinenceState(
                    AbstinenceStatus.Abstaining, TuesdayMorning.AddDays(-5).ToUnixTimeMilliseconds(), null);
            return new SubjectScreen(rig.Vm, new TrackingDisposable());
        }
    }

    private sealed class DecliningHandler(HabitType type) : IQuickActionHandler
    {
        public List<DateOnly?> Days { get; } = [];
        public HabitType HabitType => type;

        public ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
        {
            Days.Add(day);
            return new(false);
        }
    }

    private static HomeTreeRow SampleRow(Guid id, string name, HabitType? type = HabitType.Action, Guid? parent = null) =>
        new(id, parent, name, "#000000", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private static (MainWindowViewModel sut, Parts parts) CreateSut()
    {
        var query = new FakeHabitTreeQueryService();
        var nodes = new FakeHabitNodeService();
        var purge = new FakePurgeService();
        var settings = new FakeAppSettingService();
        var home = new HomeViewModel(query, nodes, purge);
        var clock = new FakeClock();
        var ats = new FakeActiveTimerService();
        var timerStatusBar = new TimerStatusBarViewModel(ats, clock, nodes, new FakeEventBus(), NullTickSourceFactory.Instance);
        var workspace = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);
        var charts = new ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(),
            new ExtensionPointRegistry(),
            new ChartBuilderViewModel(new FakeChartService(), new FakeHabitTreeQueryService(), new FakeMetricDefinitionService(),
                new FakeChartQueryService(), new ExtensionPointRegistry(), null));
        var exportLauncher = new FakeExportDialogLauncher();
        var backupLauncher = new FakeBackupDialogLauncher();
        var backupLog = new FakeBackupLogService();
        var backupHistory = new BackupHistoryViewModel(backupLog, new FakeFolderOpener());
        var settingsLauncher = new FakeSettingsWindowLauncher();
        var sut = new MainWindowViewModel(home, settings, timerStatusBar, workspace, charts, exportLauncher, backupLauncher, backupHistory, settingsLauncher, new FakeApplicationLifetime());
        return (sut, new Parts(query, nodes, home, workspace, settings, exportLauncher, settingsLauncher));
    }

    private sealed record Parts(
        FakeHabitTreeQueryService Query,
        FakeHabitNodeService Nodes,
        HomeViewModel Home,
        WorkspaceViewModel Workspace,
        FakeAppSettingService AppSettings,
        FakeExportDialogLauncher ExportLauncher,
        FakeSettingsWindowLauncher SettingsLauncher);
}
