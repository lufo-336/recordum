using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class HomeRowSectionViewModelTests
{
    private static HabitNode Habit(Guid id, HabitType type) =>
        new() { Id = id, Name = "h", NodeType = NodeType.Habit, HabitType = type };

    [Fact]
    public async Task LoadAsync_lists_type_allowed_key_figures_with_default_selected()
    {
        var prefs = new FakeHabitUiPrefsService();
        var sut = new HomeRowSectionViewModel(prefs, null);

        await sut.LoadAsync(Habit(Guid.NewGuid(), HabitType.Counter));

        sut.KeyFigureOptions.Select(o => o.Kind).Should()
            .Equal(KeyFigureKind.CounterTotal, KeyFigureKind.Streak, KeyFigureKind.None);
        sut.SelectedKeyFigure!.Kind.Should().Be(KeyFigureKind.CounterTotal);
        sut.StripOptions.Select(o => o.Strip).Should().Equal(HomeStrip.None, HomeStrip.Days7, HomeStrip.Days14);
        sut.SelectedStrip!.Strip.Should().Be(HomeStrip.Days7);
    }

    [Fact]
    public async Task Selecting_key_figure_persists_and_reload_returns_it()
    {
        var prefs = new FakeHabitUiPrefsService();
        var id = Guid.NewGuid();
        var sut = new HomeRowSectionViewModel(prefs, null);
        await sut.LoadAsync(Habit(id, HabitType.Action));

        sut.SelectedKeyFigure = sut.KeyFigureOptions.First(o => o.Kind == KeyFigureKind.DoneToday);

        // wait for the fire-and-forget ApplyAsync to settle
        await Task.Delay(20);
        prefs.Store[id].KeyFigure.Should().Be("doneToday");

        var reloaded = new HomeRowSectionViewModel(prefs, null);
        await reloaded.LoadAsync(Habit(id, HabitType.Action));
        reloaded.SelectedKeyFigure!.Kind.Should().Be(KeyFigureKind.DoneToday);
    }

    [Fact]
    public async Task Selecting_strip_persists_merged_with_existing_key_figure()
    {
        var prefs = new FakeHabitUiPrefsService();
        var id = Guid.NewGuid();
        var sut = new HomeRowSectionViewModel(prefs, null);
        await sut.LoadAsync(Habit(id, HabitType.Action));

        sut.SelectedKeyFigure = sut.KeyFigureOptions.First(o => o.Kind == KeyFigureKind.DoneToday);
        await Task.Delay(20);
        sut.SelectedStrip = sut.StripOptions.First(o => o.Strip == HomeStrip.None);
        await Task.Delay(20);

        prefs.Store[id].KeyFigure.Should().Be("doneToday"); // not clobbered by the strip write
        prefs.Store[id].Strip.Should().Be(HomeStrip.None);
    }

    [Fact]
    public void IsApplicable_only_for_habits()
    {
        var sut = new HomeRowSectionViewModel(new FakeHabitUiPrefsService(), null);
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }
}
