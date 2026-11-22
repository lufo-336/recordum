namespace Recordum.Core.Models;

public sealed class Completion
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public DateTime CompletedAt { get; set; }
    public long CompletedAtMs { get; set; }
    public DateTime? StartedAt { get; set; }
    public long? StartedAtMs { get; set; }
    public DateTime? EndedAt { get; set; }
    public long? EndedAtMs { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Notes { get; set; }
    public string? LastEditedDeviceName { get; set; }
    public bool IsRetroactive { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
    public List<CompletionValue> Values { get; set; } = [];
    public List<CompletionSegment> Segments { get; set; } = [];
}
