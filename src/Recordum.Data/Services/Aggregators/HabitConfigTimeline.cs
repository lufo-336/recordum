using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.Data.Services.Aggregators;

internal sealed class HabitConfigTimeline
{
    private readonly HabitConfig _current;
    private readonly HabitConfigHistory[] _history;
    private readonly DayBoundary _day;

    public HabitConfigTimeline(HabitConfig current, IReadOnlyList<HabitConfigHistory> history, DayBoundary day)
    {
        _current = current;
        _history = history.OrderBy(h => h.ValidFrom).ToArray();
        _day = day;
    }

    public HabitConfig ResolveFor(DateOnly date)
    {
        // L'ultimo istante del giorno secondo il confine dell'abitudine, non le 23:59:59 UTC:
        // la config che vale per un giorno è quella in vigore quando quel giorno finisce.
        var dt = _day.RangeOf(date).ToExclusive.UtcDateTime.AddTicks(-1);
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
