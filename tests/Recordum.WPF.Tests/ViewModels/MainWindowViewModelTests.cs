using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task Initialize_calls_HomeViewModel_LoadCommand_and_SettingsViewModel_InitializeAsync()
    {
        var (sut, parts) = CreateSut(themeMode: ThemeMode.Dark);

        await sut.InitializeAsync();

        sut.Home.State.Should().NotBe(HomeState.Loading);
        sut.Settings.ThemeMode.Should().Be(ThemeMode.Dark);
        parts.Query.HomeCallCount.Should().Be(1);
    }

    [Fact]
    public void Home_Settings_Editor_are_exposed_to_the_view()
    {
        var (sut, parts) = CreateSut();

        sut.Home.Should().BeSameAs(parts.Home);
        sut.Settings.Should().BeSameAs(parts.Settings);
        sut.Editor.Should().BeSameAs(parts.Editor);
    }

    [Fact]
    public async Task Selecting_a_row_loads_the_Editor_with_that_habit_node()
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

        sut.Editor.HasNode.Should().BeTrue();
        sut.Editor.Name.Should().Be("yoga");
    }

    [Fact]
    public async Task InitializeAsync_loads_EditorPaneWidth_from_AppSetting()
    {
        var (sut, parts) = CreateSut();
        parts.AppSettings.Store[MainWindowViewModel.EditorPaneWidthSettingKey] = "620";

        await sut.InitializeAsync();

        sut.EditorPaneWidth.Should().Be(620d);
    }

    [Fact]
    public async Task InitializeAsync_falls_back_to_default_when_AppSetting_missing()
    {
        var (sut, _) = CreateSut();

        await sut.InitializeAsync();

        sut.EditorPaneWidth.Should().Be(MainWindowViewModel.DefaultEditorPaneWidth);
    }

    [Fact]
    public async Task InitializeAsync_clamps_persisted_width_to_min_max()
    {
        var (sut, parts) = CreateSut();
        parts.AppSettings.Store[MainWindowViewModel.EditorPaneWidthSettingKey] = "99999";

        await sut.InitializeAsync();

        sut.EditorPaneWidth.Should().Be(MainWindowViewModel.MaxEditorPaneWidth);
    }

    [Fact]
    public async Task CommitEditorPaneWidthAsync_clamps_and_persists_via_AppSetting()
    {
        var (sut, parts) = CreateSut();
        await sut.InitializeAsync();

        sut.EditorPaneWidth = 50d; // below min — should clamp
        await sut.CommitEditorPaneWidthAsync();

        sut.EditorPaneWidth.Should().Be(MainWindowViewModel.MinEditorPaneWidth);
        parts.AppSettings.Store.Should().ContainKey(MainWindowViewModel.EditorPaneWidthSettingKey);
        parts.AppSettings.Store[MainWindowViewModel.EditorPaneWidthSettingKey]
            .Should().Be(MainWindowViewModel.MinEditorPaneWidth.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Clearing_selection_unloads_the_Editor()
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
        sut.Editor.HasNode.Should().BeTrue();

        sut.Home.SelectRowCommand.Execute(null);
        await Task.Delay(30);

        sut.Editor.HasNode.Should().BeFalse();
    }

    private static (MainWindowViewModel sut, Parts parts) CreateSut(ThemeMode themeMode = ThemeMode.System)
    {
        var query = new FakeHabitTreeQueryService();
        var nodes = new FakeHabitNodeService();
        var purge = new FakePurgeService();
        var theme = new FakeThemeService { CurrentMode = themeMode };
        var configs = new FakeHabitConfigService();
        var goals = new FakeGoalService();
        var metrics = new FakeMetricDefinitionService();
        var notif = new FakeNotificationService();
        var tags = new FakeTagService();
        var settings = new FakeAppSettingService();
        var home = new HomeViewModel(query, nodes, purge);
        var settingsVm = new SettingsViewModel(theme);
        var editor = new HabitEditorViewModel(nodes, configs, goals, metrics, notif, tags, settings);
        var timerStatusBar = new Recordum.WPF.ViewModels.Timer.TimerStatusBarViewModel(
            new FakeActiveTimerService(), new FakeClock(), nodes);
        var sut = new MainWindowViewModel(home, settingsVm, editor, settings, timerStatusBar);
        return (sut, new Parts(query, nodes, home, settingsVm, editor, settings));
    }

    private sealed record Parts(
        FakeHabitTreeQueryService Query,
        FakeHabitNodeService Nodes,
        HomeViewModel Home,
        SettingsViewModel Settings,
        HabitEditorViewModel Editor,
        FakeAppSettingService AppSettings);
}
