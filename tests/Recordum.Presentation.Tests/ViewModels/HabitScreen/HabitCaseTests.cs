using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class HabitCaseTests
{
    [Theory]
    [InlineData(HabitType.Resistance, false, 0, false, HabitCase.Abstinence)]
    [InlineData(HabitType.Journal, false, 1, false, HabitCase.Journal)]
    [InlineData(HabitType.State, false, 1, false, HabitCase.State)]
    [InlineData(HabitType.Action, false, 0, false, HabitCase.Action)]
    [InlineData(HabitType.Counter, true, 1, false, HabitCase.Session)]
    [InlineData(HabitType.Counter, false, 0, false, HabitCase.CounterTap)]
    [InlineData(HabitType.Counter, false, 1, false, HabitCase.Measured)]
    [InlineData(HabitType.Counter, false, 2, false, HabitCase.Series)]
    public void Resolve_follows_the_rules(HabitType type, bool timer, int metrics, bool repeatable, HabitCase expected)
    {
        var config = new HabitConfig
        {
            HasIntegratedTimer = timer,
            TimerMode = timer ? TimerMode.Session : null,
            Repeatable = repeatable,
        };

        HabitCases.Resolve(type, config, metrics).Should().Be(expected);
    }

    [Fact]
    public void Repeatable_single_field_is_series()
        => HabitCases.Resolve(HabitType.Counter, new HabitConfig { Repeatable = true }, 1).Should().Be(HabitCase.Series);

    [Fact]
    public void Timer_wins_over_fields()
        => HabitCases.Resolve(HabitType.Counter,
            new HabitConfig { HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch }, 2).Should().Be(HabitCase.Session);

    [Fact]
    public void A_timer_flag_without_a_mode_is_not_a_session()
        => HabitCases.Resolve(HabitType.Counter, new HabitConfig { HasIntegratedTimer = true }, 1).Should().Be(HabitCase.Measured);

    [Fact]
    public void A_missing_config_counts_as_defaults()
        => HabitCases.Resolve(HabitType.Counter, null, 0).Should().Be(HabitCase.CounterTap);
}
