using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.ViewModels.Export;
using Recordum.WPF.Views;

using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class WpfExportDialogLauncher(IServiceProvider services) : IExportDialogLauncher
{
    public async Task ShowForHabitAsync(Guid habitNodeId, string habitName)
    {
        var vm = services.GetRequiredService<ExportDialogViewModel>();
        await vm.LoadTemplatesAsync().ConfigureAwait(true);
        vm.LoadForHabit(habitNodeId, habitName);
        ShowModal(vm);
    }

    public async Task ShowForLibraryAsync()
    {
        var vm = services.GetRequiredService<ExportDialogViewModel>();
        await vm.LoadTemplatesAsync().ConfigureAwait(true);
        vm.LoadForLibrary();
        ShowModal(vm);
    }

    private static void ShowModal(ExportDialogViewModel vm)
    {
        var dlg = new ExportDialog(vm) { Owner = Application.Current?.MainWindow };
        dlg.ShowDialog();
    }
}
