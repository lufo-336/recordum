using Recordum.Core.Backup;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.Backup;

public sealed class BackupHistoryRowViewModel(BackupLog row)
{
    public Guid Id => row.Id;
    public DateTime CreatedAt => row.CreatedAt;
    public string Kind => row.Kind;
    /// <summary>Il tipo nella lingua dell'utente; un valore sconosciuto resta com'e' (F2c-6).</summary>
    public string KindDisplay => row.Kind switch
    {
        nameof(BackupKind.Manual) => Strings.Backup_Kind_Manual,
        nameof(BackupKind.Auto) => Strings.Backup_Kind_Auto,
        nameof(BackupKind.PreRestore) => Strings.Backup_Kind_PreRestore,
        _ => row.Kind,
    };
    public string Path => row.Path;
    public long SizeBytes => row.SizeBytes;
    public string SchemaVersion => row.SchemaVersion;
    public string CreatedAtDisplay => row.CreatedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    public string SizeDisplay => row.SizeBytes < 1024 ? $"{row.SizeBytes} B"
        : row.SizeBytes < 1024 * 1024 ? $"{row.SizeBytes / 1024.0:F1} KB"
        : $"{row.SizeBytes / 1048576.0:F1} MB";
}
