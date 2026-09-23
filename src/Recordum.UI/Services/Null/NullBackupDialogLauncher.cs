using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.UI.Services.Null;

/// <summary>Le dialog di backup/restore restano con la loro vista al di là di F1 (F6).</summary>
public sealed class NullBackupDialogLauncher : IBackupDialogLauncher
{
    public Task ShowBackupAsync() => Task.CompletedTask;

    public Task ShowRestoreAsync() => Task.CompletedTask;
}
