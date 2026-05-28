using Recordum.WPF.ViewModels.Backup;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeBackupDialogLauncher : IBackupDialogLauncher
{
    public int BackupShown, RestoreShown, SettingsShown;
    public Task ShowBackupAsync() { BackupShown++; return Task.CompletedTask; }
    public Task ShowRestoreAsync() { RestoreShown++; return Task.CompletedTask; }
    public Task ShowSettingsAsync() { SettingsShown++; return Task.CompletedTask; }
}
