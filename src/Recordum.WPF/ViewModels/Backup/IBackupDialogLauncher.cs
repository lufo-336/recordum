namespace Recordum.WPF.ViewModels.Backup;

public interface IBackupDialogLauncher
{
    Task ShowBackupAsync();
    Task ShowRestoreAsync();
}
