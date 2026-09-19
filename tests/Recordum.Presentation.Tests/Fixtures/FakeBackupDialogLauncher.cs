using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeBackupDialogLauncher : IBackupDialogLauncher
{
    public int BackupShown, RestoreShown;
    public Task ShowBackupAsync() { BackupShown++; return Task.CompletedTask; }
    public Task ShowRestoreAsync() { RestoreShown++; return Task.CompletedTask; }
}
