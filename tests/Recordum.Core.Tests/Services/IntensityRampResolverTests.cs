using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Xunit;

namespace Recordum.Core.Tests.Services;

public sealed class IntensityRampResolverTests
{
    private static HabitNode H() => new() { Id = Guid.NewGuid(), Name = "h", NodeType = NodeType.Habit, HabitType = HabitType.Counter };

    [Fact]
    public void No_goal_returns_fallback_breakpoints_1_3_7_8()
    {
        var r = new IntensityRampResolver();
        var ramp = r.Resolve(H(), config: null, activeGoal: null);
        Assert.Equal(1m, ramp.Step1);
        Assert.Equal(3m, ramp.Step2);
        Assert.Equal(7m, ramp.Step3);
        Assert.Equal(8m, ramp.Step4);
    }

    [Fact]
    public void Open_goal_with_target_returns_quarter_ramp()
    {
        var goal = new Goal { TargetValue = 100m, PeriodType = GoalPeriodType.Open };
        var ramp = new IntensityRampResolver().Resolve(H(), null, goal);
        Assert.Equal(25m, ramp.Step1);
        Assert.Equal(50m, ramp.Step2);
        Assert.Equal(75m, ramp.Step3);
        Assert.Equal(100m, ramp.Step4);
    }

    [Fact]
    public void Rolling_goal_normalizes_target_per_day()
    {
        var goal = new Goal { TargetValue = 70m, PeriodType = GoalPeriodType.Rolling, RollingDays = 7 };
        var ramp = new IntensityRampResolver().Resolve(H(), null, goal);
        Assert.Equal(2.5m, ramp.Step1);
        Assert.Equal(5m, ramp.Step2);
        Assert.Equal(7.5m, ramp.Step3);
        Assert.Equal(10m, ramp.Step4);
    }

    [Fact]
    public void Fixed_goal_normalizes_target_per_day_over_period_length()
    {
        var goal = new Goal
        {
            TargetValue = 30m,
            PeriodType = GoalPeriodType.Fixed,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 1, 30),
        };
        var ramp = new IntensityRampResolver().Resolve(H(), null, goal);
        Assert.Equal(0.25m, ramp.Step1);
        Assert.Equal(0.5m, ramp.Step2);
        Assert.Equal(0.75m, ramp.Step3);
        Assert.Equal(1m, ramp.Step4);
    }

    [Fact]
    public void Goal_with_zero_target_falls_back()
    {
        var goal = new Goal { TargetValue = 0m, PeriodType = GoalPeriodType.Open };
        var ramp = new IntensityRampResolver().Resolve(H(), null, goal);
        Assert.Equal(1m, ramp.Step1);
        Assert.Equal(8m, ramp.Step4);
    }

    [Fact]
    public void Goal_with_null_period_fields_falls_back()
    {
        var goal = new Goal { TargetValue = 30m, PeriodType = GoalPeriodType.Fixed };
        var ramp = new IntensityRampResolver().Resolve(H(), null, goal);
        Assert.Equal(8m, ramp.Step4);
    }
}
