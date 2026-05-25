using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class HabitConfig
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }

    public ScheduleType ScheduleType { get; set; }
    public string? ScheduleDaysJson { get; set; }
    public int? ScheduleWeeklyTarget { get; set; }
    public int? ScheduleEveryXDays { get; set; }
    public string? ScheduleMonthlyDaysJson { get; set; }

    public StreakMode StreakMode { get; set; }
    public StreakRecovery StreakRecovery { get; set; }
    public int? StreakToleranceMinutes { get; set; }

    public bool HasIntegratedTimer { get; set; }
    public TimerMode? TimerMode { get; set; }
    public int? TimerTargetSeconds { get; set; }
    public bool TimerAutoPrompt { get; set; }
    public int? RestTimerSeconds { get; set; }
    public bool SessionBaseTimer { get; set; }
    public int? SessionCountdownSeconds { get; set; }

    public bool QuickComplete { get; set; }
    public AiDataSharing AiDataSharing { get; set; } = AiDataSharing.None;

    public DateTime? StreakBrokenAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
