namespace Recordum.Core.Models;

public sealed class SegmentDefinition
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsRest { get; set; }
    /// <summary>Optional countdown length for the segment. Null = manual/lap (user advances). Set = auto-advancing interval.</summary>
    public int? DurationSeconds { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
