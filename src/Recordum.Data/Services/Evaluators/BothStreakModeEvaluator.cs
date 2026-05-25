using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Evaluators;

public sealed class BothStreakModeEvaluator(
    DailyStreakModeEvaluator daily,
    WeeklyStreakModeEvaluator weekly) : IStreakModeEvaluator
{
    public StreakMode Mode => StreakMode.Both;

    public DayVerdict Evaluate(DayContext ctx)
    {
        var d = daily.Evaluate(ctx);
        var w = weekly.Evaluate(ctx);
        if (d == DayVerdict.Misses || w == DayVerdict.Misses) return DayVerdict.Misses;
        if (d == DayVerdict.Counts || w == DayVerdict.Counts) return DayVerdict.Counts;
        return DayVerdict.Neutral;
    }
}
