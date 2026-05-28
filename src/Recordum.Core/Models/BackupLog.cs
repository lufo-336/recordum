namespace Recordum.Core.Models;

public sealed class BackupLog
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Kind { get; set; } = "Manual";
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
