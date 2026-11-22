namespace Recordum.Core.Models;

public sealed class StreakPause
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public DateOnly PauseFrom { get; set; }
    public DateOnly PauseTo { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
