using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Core.Services;

public sealed class IntensityRampResolver : IIntensityRampResolver
{
    public IntensityRamp Resolve(HabitNode node, HabitConfig? config, Goal? activeGoal)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (activeGoal is null || activeGoal.TargetValue <= 0m) return IntensityRamp.Fallback;

        var perDayTarget = NormalizeToPerDay(activeGoal);
        if (perDayTarget is null || perDayTarget <= 0m) return IntensityRamp.Fallback;

        var t = perDayTarget.Value;
        return new IntensityRamp(t * 0.25m, t * 0.5m, t * 0.75m, t);
    }

    private static decimal? NormalizeToPerDay(Goal goal) => goal.PeriodType switch
    {
        GoalPeriodType.Open => goal.TargetValue,
        GoalPeriodType.Rolling when goal.RollingDays is > 0 => goal.TargetValue / goal.RollingDays.Value,
        GoalPeriodType.Fixed when goal.PeriodStart is { } s && goal.PeriodEnd is { } e && e >= s
            => goal.TargetValue / (e.DayNumber - s.DayNumber + 1),
        _ => null,
    };
}
