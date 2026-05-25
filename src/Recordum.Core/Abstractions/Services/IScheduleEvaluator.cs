using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Services;

public interface IScheduleEvaluator
{
    bool IsScheduled(DateOnly forDate, ScheduledRules rules);
}

public sealed record ScheduledRules(
    ScheduleType Type,
    string? DaysJson,
    int? WeeklyTarget,
    int? EveryXDays,
    string? MonthlyDaysJson,
    DateOnly HabitCreatedDate);
