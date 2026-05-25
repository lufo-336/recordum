using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Services;

public interface IStreakModeEvaluator
{
    StreakMode Mode { get; }
    DayVerdict Evaluate(DayContext ctx);
}

public enum DayVerdict { Counts, Misses, Neutral }

public sealed record DayContext(
    DateOnly Date,
    bool IsScheduled,
    bool IsPaused,
    decimal CompletedValue,
    int CompletedCount,
    WeeklyPeriodState? WeekState);

public sealed record WeeklyPeriodState(
    int RequiredCompletions,
    int CompletionsSoFar,
    bool IsEndOfWeek);
