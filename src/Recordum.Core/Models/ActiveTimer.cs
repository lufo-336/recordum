using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class ActiveTimer
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public long StartedAtMs { get; set; }
    public TimerMode TimerMode { get; set; }
    public int? SegmentIndex { get; set; }
    public long? PausedAtMs { get; set; }
    public long AccumulatedPausedMs { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
