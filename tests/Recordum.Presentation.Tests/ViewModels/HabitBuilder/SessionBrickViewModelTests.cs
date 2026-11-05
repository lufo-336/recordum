using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitBuilder;

public class SessionBrickViewModelTests
{
    [Fact]
    public void Defaults_have_no_session_and_are_unavailable()
    {
        var vm = new SessionBrickViewModel();
        vm.HasSession.Should().BeFalse();
        vm.Segments.Should().BeEmpty();
        vm.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Enabling_session_seeds_work_rest_and_flags_config()
    {
        var vm = new SessionBrickViewModel();
        vm.HasSession = true;
        vm.Segments.Select(s => (s.Name, s.IsRest)).Should()
            .Equal(("Work", false), ("Rest", true));
        var cfg = new HabitConfig();
        vm.ApplyTo(cfg);
        cfg.HasIntegratedTimer.Should().BeTrue();
        cfg.TimerMode.Should().Be(TimerMode.Session);
        cfg.SessionBaseTimer.Should().BeTrue();
    }

    [Fact]
    public void Disabling_session_leaves_config_at_defaults()
    {
        var vm = new SessionBrickViewModel { HasSession = true };
        vm.HasSession = false;

        var cfg = new HabitConfig();
        vm.ApplyTo(cfg);
        cfg.HasIntegratedTimer.Should().BeFalse();
        cfg.TimerMode.Should().BeNull();
        cfg.SessionBaseTimer.Should().BeFalse();
    }

    [Fact]
    public void Disabling_session_clears_the_seeded_segments()
    {
        var vm = new SessionBrickViewModel { HasSession = true };
        vm.Segments.Should().NotBeEmpty();

        vm.HasSession = false;

        vm.Segments.Should().BeEmpty();
    }

    [Fact]
    public void BuildSegments_orders_by_list_position()
    {
        var vm = new SessionBrickViewModel { HasSession = true };
        vm.Segments.Add(new SegmentRowViewModel { Name = "Cooldown", IsRest = true });
        var nodeId = Guid.NewGuid();
        var segs = vm.BuildSegments(nodeId);
        segs.Select(s => s.SortOrder).Should().Equal(0, 1, 2);
        segs.All(s => s.HabitNodeId == nodeId).Should().BeTrue();
    }

    [Fact]
    public void BuildSegments_discards_unnamed_rows()
    {
        var vm = new SessionBrickViewModel { HasSession = true };
        vm.Segments.Add(new SegmentRowViewModel { Name = "", IsRest = false });
        var nodeId = Guid.NewGuid();

        var segs = vm.BuildSegments(nodeId);

        segs.Should().HaveCount(2); // the blank row is dropped, seeded Work/Rest remain
        segs.Select(s => s.Name).Should().Equal("Work", "Rest");
    }

    [Fact]
    public void AddSegmentCommand_appends_an_empty_row()
    {
        var vm = new SessionBrickViewModel { HasSession = true };
        var before = vm.Segments.Count;

        vm.AddSegmentCommand.Execute(null);

        vm.Segments.Should().HaveCount(before + 1);
    }

    [Fact]
    public void RemoveSegmentCommand_removes_the_given_row()
    {
        var vm = new SessionBrickViewModel { HasSession = true };
        var row = vm.Segments[0];

        vm.RemoveSegmentCommand.Execute(row);

        vm.Segments.Should().NotContain(row);
    }

    [Fact]
    public void Any_change_raises_Changed()
    {
        var vm = new SessionBrickViewModel();
        var raised = 0;
        vm.Changed += (_, _) => raised++;

        vm.HasSession = true;
        vm.AddSegmentCommand.Execute(null);

        raised.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Turning_availability_off_forces_HasSession_off_so_the_draft_never_carries_orphaned_segments()
    {
        var vm = new SessionBrickViewModel { IsAvailable = true, HasSession = true };

        vm.IsAvailable = false;

        vm.HasSession.Should().BeFalse();
        vm.Segments.Should().BeEmpty();
    }

    [Fact]
    public void Rest_and_auto_prompt_reach_the_config_only_with_a_session()
    {
        var vm = new SessionBrickViewModel { RestTimerSeconds = 45, TimerAutoPrompt = true };
        var off = new HabitConfig();
        vm.ApplyTo(off);
        off.RestTimerSeconds.Should().BeNull();
        off.TimerAutoPrompt.Should().BeFalse();

        vm.HasSession = true;
        var on = new HabitConfig();
        vm.ApplyTo(on);
        on.RestTimerSeconds.Should().Be(45);
        on.TimerAutoPrompt.Should().BeTrue();
    }

    [Fact]
    public void A_rest_that_is_not_positive_is_incomplete()
    {
        var vm = new SessionBrickViewModel { HasSession = true, RestTimerSeconds = 0 };
        vm.IsComplete.Should().BeFalse();
        vm.RestTimerSeconds = 45;
        vm.IsComplete.Should().BeTrue();
        vm.RestTimerSeconds = null;
        vm.IsComplete.Should().BeTrue();
    }
}
