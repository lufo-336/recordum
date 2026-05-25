using Recordum.Core.Models;

namespace Recordum.Data.Services.Aggregators;

internal sealed class HabitConfigTimeline
{
    private readonly HabitConfig _current;
    private readonly HabitConfigHistory[] _history;

    public HabitConfigTimeline(HabitConfig current, IReadOnlyList<HabitConfigHistory> history)
    {
        _current = current;
        _history = history.OrderBy(h => h.ValidFrom).ToArray();
    }

    public HabitConfig ResolveFor(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MaxValue);
        for (var i = 0; i < _history.Length; i++)
        {
            var h = _history[i];
            var until = h.ValidUntil ?? DateTime.MaxValue;
            if (h.ValidFrom <= dt && dt < until)
                return Project(h);
        }
        return _current;
    }

    private static HabitConfig Project(HabitConfigHistory h) => new()
    {
        Id = Guid.Empty, HabitNodeId = h.HabitNodeId,
        ScheduleType = h.ScheduleType, ScheduleDaysJson = h.ScheduleDaysJson,
        ScheduleWeeklyTarget = h.ScheduleWeeklyTarget, ScheduleEveryXDays = h.ScheduleEveryXDays,
        ScheduleMonthlyDaysJson = h.ScheduleMonthlyDaysJson,
        StreakMode = h.StreakMode, StreakRecovery = h.StreakRecovery,
        StreakToleranceMinutes = h.StreakToleranceMinutes,
        HasIntegratedTimer = h.HasIntegratedTimer, TimerMode = h.TimerMode,
        TimerTargetSeconds = h.TimerTargetSeconds, TimerAutoPrompt = h.TimerAutoPrompt,
        RestTimerSeconds = h.RestTimerSeconds, SessionBaseTimer = h.SessionBaseTimer,
        SessionCountdownSeconds = h.SessionCountdownSeconds,
    };
}
