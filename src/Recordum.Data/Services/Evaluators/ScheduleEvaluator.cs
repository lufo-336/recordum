using System.Text.Json;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Evaluators;

public sealed class ScheduleEvaluator : IScheduleEvaluator
{
    public bool IsScheduled(DateOnly forDate, ScheduledRules rules)
    {
        if (forDate < rules.HabitCreatedDate) return false;
        return rules.Type switch
        {
            ScheduleType.Daily => true,
            ScheduleType.WeeklyX => true,
            ScheduleType.Custom => true,
            ScheduleType.SpecificDays => MatchesDayMask(forDate, rules.DaysJson),
            ScheduleType.EveryXDays => rules.EveryXDays is { } x && x > 0
                && (forDate.DayNumber - rules.HabitCreatedDate.DayNumber) % x == 0,
            ScheduleType.Monthly => MatchesMonthDays(forDate, rules.MonthlyDaysJson),
            _ => false
        };
    }

    private static bool MatchesDayMask(DateOnly d, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var days = JsonSerializer.Deserialize<string[]>(json);
            if (days is null || days.Length == 0) return false;
            var key = d.DayOfWeek switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                DayOfWeek.Sunday => "Sun",
                _ => ""
            };
            return Array.Exists(days, x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }

    private static bool MatchesMonthDays(DateOnly d, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var days = JsonSerializer.Deserialize<int[]>(json);
            if (days is null || days.Length == 0) return false;
            return Array.IndexOf(days, d.Day) >= 0;
        }
        catch (JsonException) { return false; }
    }
}
