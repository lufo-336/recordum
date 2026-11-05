using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

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

    [Fact]
    public void Build_carries_name_and_rolling_period()
    {
        var vm = new GoalBrickViewModel
        {
            HasGoal = true, Target = 2m, Name = "  Settimana  ",
            PeriodType = GoalPeriodType.Rolling, RollingDays = 7,
        };
        var goal = vm.Build(Guid.NewGuid(), "Acqua", null)!;
        goal.Name.Should().Be("Settimana");
        goal.PeriodType.Should().Be(GoalPeriodType.Rolling);
        goal.RollingDays.Should().Be(7);
    }

    [Fact]
    public void Build_falls_back_to_the_habit_name_and_drops_stale_rolling_days_when_open()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m, Name = " ", RollingDays = 7 };
        var goal = vm.Build(Guid.NewGuid(), "Acqua", null)!;
        goal.Name.Should().Be("Acqua");
        goal.PeriodType.Should().Be(GoalPeriodType.Open);
        goal.RollingDays.Should().BeNull();
    }

    [Fact]
    public void Fixed_is_never_complete_because_the_creator_has_no_dates()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m, PeriodType = GoalPeriodType.Fixed };
        vm.IsComplete.Should().BeFalse();
    }

    // ---- Fix round 1 (Ruling 7): "Conta sugli ultimi [N] giorni" in Avanzate ----------------------------

    [Fact]
    public void The_window_row_makes_the_goal_rolling_with_a_number_and_open_without()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m };
        vm.RollingWindowDays.Should().BeNull();

        vm.RollingWindowDays = 7;
        vm.PeriodType.Should().Be(GoalPeriodType.Rolling);
        vm.RollingDays.Should().Be(7);

        vm.RollingWindowDays = null;
        vm.PeriodType.Should().Be(GoalPeriodType.Open);
        vm.RollingDays.Should().BeNull();
        vm.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void The_window_row_reads_what_a_load_wrote_and_says_so()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m };
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        vm.PeriodType = GoalPeriodType.Rolling;
        vm.RollingDays = 14;
        vm.RollingWindowDays.Should().Be(14);
        raised.Should().Contain(nameof(GoalBrickViewModel.RollingWindowDays));

        vm.PeriodType = GoalPeriodType.Fixed;
        vm.RollingWindowDays.Should().BeNull("solo un periodo mobile ha una finestra");
    }

    [Fact]
    public void A_zero_window_is_kept_as_typed_and_blocks()
    {
        var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m, RollingWindowDays = 0 };
        vm.PeriodType.Should().Be(GoalPeriodType.Rolling);
        vm.IsComplete.Should().BeFalse("cio' che si salva e' cio' che si vede: uno zero non diventa 'aperto' in silenzio");
    }
}
