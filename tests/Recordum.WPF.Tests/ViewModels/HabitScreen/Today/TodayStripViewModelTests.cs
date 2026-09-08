using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Today;

public class TodayStripViewModelTests
{
    private static TodayStripViewModel NewSut()
        => new(new FakeAbstinenceService(), new FakeCompletionService(), new FakeStreakAggregator(),
               new FakeHourlyDistributionQueryService(), new FakeMetricDefinitionService(),
               new FakeReferenceData(), new FakeGoalService(), new FakeHabitUiPrefsService(),
               new FakeClock(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task LoadAsync_on_a_group_clears_the_strip_without_throwing()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Group, HabitType = null, Name = "g" };

        var act = async () => await sut.LoadAsync(node, DayBoundary.Utc);

        await act.Should().NotThrowAsync();
        sut.Content.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_on_a_resistance_habit_builds_resistance_content()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "r" };

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.Content.Should().BeOfType<ResistanceTodayContent>();
    }

    [Fact]
    public async Task LoadAsync_on_an_action_habit_builds_action_content()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a" };

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.Content.Should().BeOfType<ActionTodayContent>();
    }

    [Fact]
    public async Task LoadAsync_on_a_counter_habit_builds_counter_content()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" };

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.Content.Should().BeOfType<CounterTodayContent>();
    }

    [Fact]
    public async Task LoadAsync_on_a_state_habit_builds_state_content()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" };

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.Content.Should().BeOfType<StateTodayContent>();
    }

    [Fact]
    public async Task LoadAsync_on_a_journal_habit_builds_journal_content()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.Content.Should().BeOfType<JournalTodayContent>();
    }

    [Fact]
    public async Task Content_navigation_bubbles_through_the_strip()
    {
        var sut = NewSut();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        await sut.LoadAsync(node, DayBoundary.Utc);
        TodayContentAction? bubbled = null;
        sut.ActionRequested += (_, a) => bubbled = a;

        ((JournalTodayContent)sut.Content!).PrimaryCommand.Execute(null);

        bubbled.Should().Be(TodayContentAction.OpenDiario);
    }

    [Fact]
    public async Task ShowTodayStrip_is_true_for_a_habit_false_for_a_group_and_when_unloaded()
    {
        var (screen, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        screen.ShowTodayStrip.Should().BeFalse();   // nothing loaded

        var habit = Guid.NewGuid();
        nodes.Store[habit] = new HabitNode { Id = habit, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h", Color = "#808080" };
        await screen.LoadAsync(habit);
        screen.ShowTodayStrip.Should().BeTrue();

        var group = Guid.NewGuid();
        nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "g", Color = "#808080" };
        await screen.LoadAsync(group);
        screen.ShowTodayStrip.Should().BeFalse();

        await screen.LoadAsync(null);
        screen.ShowTodayStrip.Should().BeFalse();
    }
}
