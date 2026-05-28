using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.Backup;

public sealed class BackupHistoryRowViewModel(BackupLog row)
{
    public Guid Id => row.Id;
    public DateTime CreatedAt => row.CreatedAt;
    public string Kind => row.Kind;
    public string Path => row.Path;
    public long SizeBytes => row.SizeBytes;
    public string SchemaVersion => row.SchemaVersion;
    public string CreatedAtDisplay => row.CreatedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    public string SizeDisplay => row.SizeBytes < 1024 ? $"{row.SizeBytes} B"
        : row.SizeBytes < 1024 * 1024 ? $"{row.SizeBytes / 1024.0:F1} KB"
        : $"{row.SizeBytes / 1048576.0:F1} MB";
}
