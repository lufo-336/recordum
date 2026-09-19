namespace Recordum.Presentation.ViewModels.Backup;

public interface IBackupDialogLauncher
{
    Task ShowBackupAsync();
    Task ShowRestoreAsync();
}
