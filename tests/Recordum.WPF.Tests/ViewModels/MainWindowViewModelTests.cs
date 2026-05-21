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
        var sut = new MainWindowViewModel(home, settingsVm, editor);
        return (sut, new Parts(query, nodes, home, settingsVm, editor));
    }

    private sealed record Parts(
        FakeHabitTreeQueryService Query,
        FakeHabitNodeService Nodes,
        HomeViewModel Home,
        SettingsViewModel Settings,
        HabitEditorViewModel Editor);
}
