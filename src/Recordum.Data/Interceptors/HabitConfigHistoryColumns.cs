namespace Recordum.Data.Interceptors;

public static class HabitConfigHistoryColumns
{
    public static readonly IReadOnlyList<string> Excluded = new[]
    {
        "Id","HabitNodeId","CreatedAt","UpdatedAt","UpdatedAtMs",
        "QuickComplete","AiDataSharing","StreakBrokenAt","NoteMarkerColor"
    };

    public static readonly IReadOnlyList<string> Versioned = new[]
    {
        "ScheduleType","ScheduleDaysJson","ScheduleWeeklyTarget","ScheduleEveryXDays","ScheduleMonthlyDaysJson",
        "StreakMode","StreakRecovery","StreakToleranceMinutes",
        "HasIntegratedTimer","TimerMode","TimerTargetSeconds","TimerAutoPrompt",
        "RestTimerSeconds","SessionBaseTimer","SessionCountdownSeconds","SessionRepeatCount",
        "Repeatable","SeriesLabelsJson"
    };
}
