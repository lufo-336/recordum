namespace Recordum.Core.Models;

public sealed class Chart
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ChartType { get; set; } = "line";
    public string ConfigJson { get; set; } = "{}";
    public string? CreatedByPluginId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public List<ChartSeries> Series { get; set; } = [];
}
