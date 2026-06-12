using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Web.WebView2.Core;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.Settings;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

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
    public void Home_and_Editor_are_exposed_to_the_view()
    {
        var (sut, parts) = CreateSut();

        sut.Home.Should().BeSameAs(parts.Home);
        sut.Editor.Should().BeSameAs(parts.Editor);
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
    public async Task Selecting_a_row_loads_the_HabitScreen_not_the_Editor()
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

        sut.HabitScreen.HasNode.Should().BeTrue();
        sut.HabitScreen.Name.Should().Be("yoga");
        sut.Editor.HasNode.Should().BeFalse();
    }

    [Fact]
    public async Task ShowHome_deselects_the_row_and_closes_the_workspace()
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
        sut.Home.SelectedRow.Should().NotBeNull();

        sut.ShowHomeCommand.Execute(null);

        sut.Home.SelectedRow.Should().BeNull();
        sut.IsHomeWorkspace.Should().BeTrue();
    }

    [Fact]
    public async Task HabitScreen_OpenConfigure_loads_the_Editor()
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

        sut.HabitScreen.OpenConfigureCommand.Execute(null);
        await Task.Delay(30);

        sut.Editor.HasNode.Should().BeTrue();
        sut.Editor.Name.Should().Be("yoga");
    }

    [Fact]
    public async Task InitializeAsync_loads_HabitScreenWidth_from_AppSetting()
    {
        var (sut, parts) = CreateSut();
        parts.AppSettings.Store[MainWindowViewModel.HabitScreenWidthSettingKey] = "600";

        await sut.InitializeAsync();

        sut.HabitScreenWidth.Should().Be(600d);
    }

    [Fact]
    public async Task CommitHabitScreenWidthAsync_clamps_and_persists()
    {
        var (sut, parts) = CreateSut();
        await sut.InitializeAsync();

        sut.HabitScreenWidth = 50d;
        await sut.CommitHabitScreenWidthAsync();

        sut.HabitScreenWidth.Should().Be(MainWindowViewModel.MinHabitScreenWidth);
        parts.AppSettings.Store[MainWindowViewModel.HabitScreenWidthSettingKey]
            .Should().Be(MainWindowViewModel.MinHabitScreenWidth.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    // EditorPaneWidth plumbing removed with the standalone editor pane: the editor now lives in
    // the HabitScreen's Configura column (nav-model part 1).

    [Fact]
    public async Task Clearing_selection_unloads_HabitScreen_and_Editor()
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
        sut.HabitScreen.OpenConfigureCommand.Execute(null);
        await Task.Delay(30);
        sut.HabitScreen.HasNode.Should().BeTrue();
        sut.Editor.HasNode.Should().BeTrue();

        sut.Home.SelectRowCommand.Execute(null);
        await Task.Delay(30);

        sut.HabitScreen.HasNode.Should().BeFalse();
        sut.Editor.HasNode.Should().BeFalse();
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
    public async Task HabitScreen_ExportRequested_invokes_launcher_for_current_habit()
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

        sut.HabitScreen.ExportCommand.Execute(null);
        await Task.Delay(30);

        Assert.Equal(1, parts.ExportLauncher.HabitCount);
        Assert.Equal(id, parts.ExportLauncher.LastHabitId);
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
    public async Task Switching_habit_with_configura_open_rebinds_the_editor()
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
        sut.HabitScreen.IsConfiguraOpen = true;
        await Task.Delay(30);
        sut.Editor.Node!.Id.Should().Be(a);

        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == b);
        await Task.Delay(30);

        sut.Editor.Node!.Id.Should().Be(b);
    }

    [Fact]
    public async Task Creating_a_group_opens_configura_on_the_new_group()
    {
        var (sut, parts) = CreateSut();
        parts.Nodes.OnCreate = draft => parts.Query.HomeRows.Add(SampleRow(draft.Id, draft.Name, type: null));
        await sut.InitializeAsync();

        await sut.Home.CreateGroupCommand.ExecuteAsync(null);
        await Task.Delay(30);

        sut.HabitScreen.IsConfiguraOpen.Should().BeTrue();
        sut.Editor.Node!.NodeType.Should().Be(NodeType.Group);
    }

    [Fact]
    public async Task Panoramica_child_click_selects_the_habit_in_home()
    {
        var (sut, parts) = CreateSut();
        var g = Guid.NewGuid();
        var h = Guid.NewGuid();
        parts.Nodes.Store[g] = new HabitNode { Id = g, NodeType = NodeType.Group, Name = "G", Color = "#808080" };
        parts.Nodes.Store[h] = new HabitNode { Id = h, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h", Color = "#808080" };
        parts.Query.HomeRows.Add(SampleRow(g, "G", type: null));
        parts.Query.HomeRows.Add(SampleRow(h, "h", parent: g));
        await sut.InitializeAsync();
        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == g);
        await Task.Delay(30);

        sut.HabitScreen.Panoramica.SelectChildCommand.Execute(sut.HabitScreen.Panoramica.Children.Single());
        await Task.Delay(30);

        sut.Home.SelectedRow!.HabitNodeId.Should().Be(h);
    }

    [Fact]
    public async Task Closing_the_last_open_column_deselects_the_row()
    {
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", Color = "#808080" };
        parts.Query.HomeRows.Add(SampleRow(id, "a"));
        await sut.InitializeAsync();
        sut.Home.SelectedRow = sut.Home.Rows.Single();
        await Task.Delay(30);            // Cronologia aperta di default

        sut.HabitScreen.IsCronologiaOpen = false;
        await Task.Delay(30);

        sut.Home.SelectedRow.Should().BeNull();
    }

    private static HomeTreeRow SampleRow(Guid id, string name, HabitType? type = HabitType.Action, Guid? parent = null) =>
        new(id, parent, name, "#000000", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private static (MainWindowViewModel sut, Parts parts) CreateSut()
    {
        var query = new FakeHabitTreeQueryService();
        var nodes = new FakeHabitNodeService();
        var purge = new FakePurgeService();
        var configs = new FakeHabitConfigService();
        var goals = new FakeGoalService();
        var metrics = new FakeMetricDefinitionService();
        var notif = new FakeNotificationService();
        var tags = new FakeTagService();
        var settings = new FakeAppSettingService();
        var home = new HomeViewModel(query, nodes, purge);
        var editor = new HabitEditorViewModel(nodes, configs, goals, metrics, notif, tags, settings, new FakeReferenceData(), new RecentColorStore(settings));
        var clock = new FakeClock();
        var ats = new FakeActiveTimerService();
        var completions = new FakeCompletionService();
        var timerStatusBar = new Recordum.WPF.ViewModels.Timer.TimerStatusBarViewModel(ats, clock, nodes);
        var habitScreen = new Recordum.WPF.ViewModels.HabitScreen.HabitScreenViewModel(
            nodes, completions, ats, metrics, new FakeReferenceData(), configs, new FakeNoteService(),
            goals, new Recordum.Core.Services.IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            clock, new FakeEventBus(), new FakeWebView2Availability(), settings, editor, query);
        var charts = new Recordum.WPF.ViewModels.Charts.ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(),
            new Recordum.Plugins.ExtensionPoints.ExtensionPointRegistry(),
            new Recordum.WPF.ViewModels.Charts.ChartBuilderViewModel(new FakeChartService()));
        var exportLauncher = new FakeExportDialogLauncher();
        var backupLauncher = new FakeBackupDialogLauncher();
        var backupLog = new FakeBackupLogService();
        var backupHistory = new Recordum.WPF.ViewModels.Backup.BackupHistoryViewModel(backupLog);
        var settingsLauncher = new FakeSettingsWindowLauncher();
        var sut = new MainWindowViewModel(home, settings, timerStatusBar, habitScreen, charts, exportLauncher, backupLauncher, backupHistory, settingsLauncher);
        return (sut, new Parts(query, nodes, home, editor, settings, exportLauncher, settingsLauncher));
    }

    private sealed record Parts(
        FakeHabitTreeQueryService Query,
        FakeHabitNodeService Nodes,
        HomeViewModel Home,
        HabitEditorViewModel Editor,
        FakeAppSettingService AppSettings,
        FakeExportDialogLauncher ExportLauncher,
        FakeSettingsWindowLauncher SettingsLauncher);
}
