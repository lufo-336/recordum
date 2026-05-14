using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public NotifyType NotifyType { get; set; }
    public TimeOnly? NotifyTime { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
