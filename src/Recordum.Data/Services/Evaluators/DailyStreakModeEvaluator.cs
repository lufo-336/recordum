using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Evaluators;

public sealed class DailyStreakModeEvaluator : IStreakModeEvaluator
{
    public StreakMode Mode => StreakMode.Daily;

    public DayVerdict Evaluate(DayContext ctx)
    {
        if (!ctx.IsScheduled) return DayVerdict.Neutral;
        return ctx.CompletedCount > 0 ? DayVerdict.Counts : DayVerdict.Misses;
    }
}
