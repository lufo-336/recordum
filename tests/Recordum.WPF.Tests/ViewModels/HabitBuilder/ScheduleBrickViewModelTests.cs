using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitBuilder;

public class ScheduleBrickViewModelTests
{
    [Fact]
    public void Defaults_match_seed_shape()
    {
        var vm = new ScheduleBrickViewModel();
        vm.ScheduleType.Should().Be(ScheduleType.Daily);
        vm.StreakMode.Should().Be(StreakMode.Daily);
        vm.StreakRecovery.Should().Be(StreakRecovery.GraceDay);
        vm.IsExpanded.Should().BeFalse();
        vm.Days.Should().HaveCount(7);
    }

    [Fact]
    public void ApplyTo_writes_specific_days_in_scheduling_section_format()
    {
        var vm = new ScheduleBrickViewModel { ScheduleType = ScheduleType.SpecificDays };
        vm.Days.Single(d => d.DayKey == "Mon").IsSelected = true;
        vm.Days.Single(d => d.DayKey == "Fri").IsSelected = true;
        var cfg = new HabitConfig();
        vm.ApplyTo(cfg);
        cfg.ScheduleType.Should().Be(ScheduleType.SpecificDays);
        cfg.ScheduleDaysJson.Should().Be("""["Mon","Fri"]""");
    }

    [Fact]
    public void ApplyTo_clears_fields_of_other_schedule_types()
    {
        var vm = new ScheduleBrickViewModel { ScheduleType = ScheduleType.WeeklyX, WeeklyTarget = 3 };
        var cfg = new HabitConfig { ScheduleDaysJson = "[\"Mon\"]", ScheduleEveryXDays = 2 };
        vm.ApplyTo(cfg);
        cfg.ScheduleWeeklyTarget.Should().Be(3);
        cfg.ScheduleDaysJson.Should().BeNull();
        cfg.ScheduleEveryXDays.Should().BeNull();
    }

    [Fact]
    public void Any_change_raises_Changed_and_updates_Summary()
    {
        var vm = new ScheduleBrickViewModel();
        var raised = 0;
        vm.Changed += (_, _) => raised++;
        var before = vm.Summary;
        vm.StreakRecovery = StreakRecovery.None;
        raised.Should().BeGreaterThan(0);
        vm.Summary.Should().NotBe(before);
    }

    [Fact]
    public void Summary_localizes_streak_mode_and_recovery_instead_of_the_raw_enum_name()
    {
        // F0-11: never show a raw enum name — Occurrences.ToString() would read "Occurrences", the
        // localized label reads "By occurrences" (Strings.HabitBuilder_StreakMode_Occurrences), so a
        // regression back to StreakMode.ToString() fails this even under the default (EN) culture.
        var vm = new ScheduleBrickViewModel { StreakMode = StreakMode.Occurrences, StreakRecovery = StreakRecovery.Retroactive };
        vm.Summary.Should().Contain(Strings.HabitBuilder_StreakMode_Occurrences);
        vm.Summary.Should().Contain(Strings.HabitBuilder_Recovery_Retroactive);
    }
}
