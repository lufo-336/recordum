using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class ScaleTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public ScalePrecision Precision { get; set; } = ScalePrecision.Integer;
    public bool IsOfficial { get; set; }
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public List<MetricDefinition> Metrics { get; set; } = [];
    public List<ScaleAnchor> Anchors { get; set; } = [];
}
