namespace Recordum.Core.Models;

public sealed class CompletionSegment
{
    public Guid Id { get; set; }
    public Guid CompletionId { get; set; }
    public Guid SegmentDefinitionId { get; set; }
    public int SegmentIndex { get; set; }
    public DateTime StartedAt { get; set; }
    public long StartedAtMs { get; set; }
    public DateTime? EndedAt { get; set; }
    public long? EndedAtMs { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsManualEdit { get; set; }
    public string? Note { get; set; }

    public Completion Completion { get; set; } = null!;
    public SegmentDefinition SegmentDefinition { get; set; } = null!;
    public List<CompletionValue> Values { get; set; } = [];
}
