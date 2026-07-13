using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Today;

public class TodayStripViewModelTests
{
    [Fact]
    public async Task LoadAsync_on_a_type_with_no_content_yet_sets_null_without_throwing()
    {
        var sut = new TodayStripViewModel();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };

        var act = async () => await sut.LoadAsync(node);

        await act.Should().NotThrowAsync();
        sut.Content.Should().BeNull();
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
