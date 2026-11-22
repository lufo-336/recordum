namespace Recordum.Core.Models;

public sealed class CompletionValue
{
    public Guid Id { get; set; }
    public Guid CompletionId { get; set; }
    public Guid MetricDefinitionId { get; set; }
    public Guid? CompletionSegmentId { get; set; }
    public int SeriesIndex { get; set; }
    public long? ValueInteger { get; set; }
    public decimal? ValueDecimal { get; set; }
    public int? ValueSeconds { get; set; }
    public string? ValueText { get; set; }
    public long UpdatedAtMs { get; set; }

    public Completion Completion { get; set; } = null!;
    public MetricDefinition MetricDefinition { get; set; } = null!;
    public CompletionSegment? CompletionSegment { get; set; }
}
