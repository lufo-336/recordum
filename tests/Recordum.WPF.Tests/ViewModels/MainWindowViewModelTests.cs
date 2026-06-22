using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.Charts;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.Settings;
using Recordum.WPF.ViewModels.Timer;
using Recordum.WPF.ViewModels.Workspace;
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
    public async Task ShowHome_returns_to_the_full_width_tree()
    {
        // Model A: ⌂ deactivates the active tab (Panoramica) and deselects → the tree fills the window.
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "yoga", Color = "#000000" };
        parts.Query.HomeRows.Add(SampleRow(id, "yoga"));
        await sut.InitializeAsync();
        sut.Home.SelectRowCommand.Execute(sut.Home.Rows.First());
        await Task.Delay(30);
        sut.Workspace.IsPanoramicaActive.Should().BeFalse();

        sut.ShowHomeCommand.Execute(null);

        sut.Workspace.IsPanoramicaActive.Should().BeTrue();
        sut.Home.SelectedRow.Should().BeNull();
        sut.IsHomeWorkspace.Should().BeTrue();
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
        var timerStatusBar = new TimerStatusBarViewModel(ats, clock, nodes);
        var workspace = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);
        var charts = new ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(),
            new ExtensionPointRegistry(),
            new ChartBuilderViewModel(new FakeChartService()));
        var exportLauncher = new FakeExportDialogLauncher();
        var backupLauncher = new FakeBackupDialogLauncher();
        var backupLog = new FakeBackupLogService();
        var backupHistory = new BackupHistoryViewModel(backupLog);
        var settingsLauncher = new FakeSettingsWindowLauncher();
        var sut = new MainWindowViewModel(home, settings, timerStatusBar, workspace, charts, exportLauncher, backupLauncher, backupHistory, settingsLauncher);
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
