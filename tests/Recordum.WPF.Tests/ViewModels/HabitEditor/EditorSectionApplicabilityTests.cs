using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class EditorSectionApplicabilityTests
{
    [Fact]
    public void Scheduling_is_hidden_for_resistance()
    {
        var s = new SchedulingSectionViewModel(new FakeHabitConfigService());
        s.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeFalse();
        s.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
    }

    [Fact]
    public void Streak_is_hidden_for_resistance()
    {
        var s = new StreakSectionViewModel(new FakeHabitConfigService());
        s.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeFalse();
        s.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
    }
}
