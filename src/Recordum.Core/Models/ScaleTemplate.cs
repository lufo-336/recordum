namespace Recordum.Core.Models;

public sealed class ScaleTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public string LabelsJson { get; set; } = "[]";
    public bool IsOfficial { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<MetricDefinition> Metrics { get; set; } = [];
}
