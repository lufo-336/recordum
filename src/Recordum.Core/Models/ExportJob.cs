using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class ExportJob
{
    public Guid Id { get; set; }
    public Guid? ExportTemplateId { get; set; }
    public ExportFormat Format { get; set; }
    public string? FilePath { get; set; }
    public string? FilterJson { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ExportTemplate? ExportTemplate { get; set; }
}
