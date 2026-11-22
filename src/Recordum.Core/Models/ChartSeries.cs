namespace Recordum.Core.Models;

public sealed class ChartSeries
{
    public Guid Id { get; set; }
    public Guid ChartId { get; set; }
    public Guid HabitNodeId { get; set; }
    public Guid? MetricDefinitionId { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public long UpdatedAtMs { get; set; }

    public Chart Chart { get; set; } = null!;
    public HabitNode HabitNode { get; set; } = null!;
    public MetricDefinition? MetricDefinition { get; set; }
}
