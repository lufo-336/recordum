using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitBuilder;

public class GoalBrickViewModelTests
{
    [Fact]
    public void Defaults_have_no_goal_and_open_period()
    {
        var vm = new GoalBrickViewModel();
        vm.HasGoal.Should().BeFalse();
        vm.Target.Should().BeNull();
        vm.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Build_returns_null_when_goal_is_off()
    {
        var vm = new GoalBrickViewModel { HasGoal = false, Target = 2m };
        vm.Build(Guid.NewGuid(), "Acqua", null).Should().BeNull();
    }

    [Fact]
    public void Build_returns_null_when_target_is_missing_or_not_positive()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = null };
        vm.Build(Guid.NewGuid(), "Acqua", null).Should().BeNull();

        vm.Target = 0m;
        vm.Build(Guid.NewGuid(), "Acqua", null).Should().BeNull();

        vm.Target = -1m;
        vm.Build(Guid.NewGuid(), "Acqua", null).Should().BeNull();
    }

    [Fact]
    public void Build_returns_goal_bound_to_habit_name_and_first_metric()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m };
        var metricId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var goal = vm.Build(nodeId, "Acqua", metricId);
        goal.Should().NotBeNull();
        goal!.Name.Should().Be("Acqua");           // CR9: mai literal hardcoded
        goal.HabitNodeId.Should().Be(nodeId);
        goal.MetricDefinitionId.Should().Be(metricId);
        goal.TargetValue.Should().Be(2m);
    }

    [Fact]
    public void Build_accepts_a_null_metric_id_for_a_tap_counter()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 5m };
        var goal = vm.Build(Guid.NewGuid(), "Push-up", null);
        goal.Should().NotBeNull();
        goal!.MetricDefinitionId.Should().BeNull();
    }

    [Fact]
    public void Any_change_raises_Changed()
    {
        var vm = new GoalBrickViewModel();
        var raised = 0;
        vm.Changed += (_, _) => raised++;

        vm.HasGoal = true;
        vm.Target = 3m;
        vm.Target = 7m;

        raised.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Turning_availability_off_forces_HasGoal_off_so_the_draft_never_carries_an_orphaned_goal()
    {
        var vm = new GoalBrickViewModel { IsAvailable = true, HasGoal = true, Target = 4m };

        vm.IsAvailable = false;

        vm.HasGoal.Should().BeFalse();
    }
}
