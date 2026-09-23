using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>Il file dialog del backup resta con `StorageProvider` al di là di F1 (F6).</summary>
public sealed class NullBackupSaveFileDialog : IBackupSaveFileDialog
{
    public string? Prompt(string defaultFileName, string filter) => null;
}
