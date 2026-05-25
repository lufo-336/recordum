using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Evaluators;

public sealed class OccurrencesStreakModeEvaluator : IStreakModeEvaluator
{
    public StreakMode Mode => StreakMode.Occurrences;

    public DayVerdict Evaluate(DayContext ctx)
        => ctx.CompletedCount > 0 ? DayVerdict.Counts : DayVerdict.Neutral;
}
