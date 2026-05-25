using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Evaluators;

public sealed class WeeklyStreakModeEvaluator : IStreakModeEvaluator
{
    public StreakMode Mode => StreakMode.Weekly;

    public DayVerdict Evaluate(DayContext ctx)
    {
        if (ctx.WeekState is not { IsEndOfWeek: true } ws) return DayVerdict.Neutral;
        return ws.CompletionsSoFar >= ws.RequiredCompletions ? DayVerdict.Counts : DayVerdict.Misses;
    }
}
