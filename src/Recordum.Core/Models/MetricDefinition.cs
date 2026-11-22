using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class MetricDefinition
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MetricType Type { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public Guid? ScaleTemplateId { get; set; }
    public int SortOrder { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    public ScaleTemplate? ScaleTemplate { get; set; }
}
