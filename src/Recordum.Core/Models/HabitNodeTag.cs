namespace Recordum.Core.Models;

public sealed class HabitNodeTag
{
    public Guid HabitNodeId { get; set; }
    public Guid TagId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
