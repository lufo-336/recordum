using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

/// <summary>
/// Section visibility must react to changing the node/habit type *inside* the editor,
/// not only at load time. A blank habit is created as Action; switching it to Counter
/// must reveal the Metrics/Timer sections so the user can add units/quantities/timer.
/// Regression for the Fase-4 "Configura non applica le modifiche" report.
/// </summary>
public class SectionVisibilityReactivityTests
{
    private static HabitEditorViewModel CreateSut(FakeHabitNodeService nodes)
    {
        var fakeSettings = new FakeAppSettingService();
        return new(
            nodes,
            new FakeHabitConfigService(),
            new FakeGoalService(),
            new FakeMetricDefinitionService(),
            new FakeNotificationService(),
            new FakeTagService(),
            fakeSettings,
            new FakeReferenceData(),
            new RecentColorStore(fakeSettings));
    }

    private static (HabitEditorViewModel Sut, Guid Id) Load(NodeType node, HabitType? habit)
    {
        var nodes = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = node, HabitType = habit, Name = "Blank" };
        var sut = CreateSut(nodes);
        sut.LoadAsync(id).GetAwaiter().GetResult();
        return (sut, id);
    }

    [Fact]
    public void Switching_action_to_counter_reveals_metrics_and_timer()
    {
        var (sut, _) = Load(NodeType.Habit, HabitType.Action);

        sut.Metrics.IsVisible.Should().BeFalse("Action habits have no metrics section");
        sut.Timer.IsVisible.Should().BeFalse("Action habits have no timer section");

        sut.SelectedHabitType = HabitType.Counter;

        sut.Metrics.IsVisible.Should().BeTrue("switching to Counter must reveal the metrics section");
        sut.Timer.IsVisible.Should().BeTrue("switching to Counter must reveal the timer section");
    }

    [Fact]
    public void Switching_habit_to_group_hides_habit_only_sections()
    {
        var (sut, _) = Load(NodeType.Habit, HabitType.Counter);

        sut.Scheduling.IsVisible.Should().BeTrue();
        sut.Metrics.IsVisible.Should().BeTrue();

        sut.SelectedNodeType = NodeType.Group;

        sut.Scheduling.IsVisible.Should().BeFalse("groups have no scheduling section");
        sut.Metrics.IsVisible.Should().BeFalse("groups have no metrics section");
    }
}
