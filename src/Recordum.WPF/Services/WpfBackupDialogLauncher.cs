using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.Views.Backup;

namespace Recordum.WPF.Services;

public sealed class WpfBackupDialogLauncher(IServiceProvider services) : IBackupDialogLauncher
{
    public Task ShowBackupAsync()
    {
        var vm = services.GetRequiredService<BackupDialogViewModel>();
        var dlg = new BackupDialog(vm) { Owner = Application.Current?.MainWindow };
        dlg.ShowDialog();
        return Task.CompletedTask;
    }

    public Task ShowRestoreAsync()
    {
        var vm = services.GetRequiredService<RestoreDialogViewModel>();
        var dlg = new RestoreDialog(vm) { Owner = Application.Current?.MainWindow };
        if (dlg.ShowDialog() == true)
            Application.Current?.Shutdown(0);
        return Task.CompletedTask;
    }

    public async Task ShowSettingsAsync()
    {
        var vm = services.GetRequiredService<BackupSettingsViewModel>();
        await vm.LoadAsync().ConfigureAwait(true);
        var dlg = new BackupSettingsDialog(vm) { Owner = Application.Current?.MainWindow };
        dlg.ShowDialog();
    }
}
