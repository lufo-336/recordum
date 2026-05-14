namespace Recordum.Core.Models;

public sealed class ExportTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public bool IsOfficial { get; set; }
    public string? CreatedByPluginId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public List<ExportJob> Jobs { get; set; } = [];
}
