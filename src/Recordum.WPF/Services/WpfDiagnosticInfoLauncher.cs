using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels.DiagnosticInfo;
using Recordum.WPF.Views;

namespace Recordum.WPF.Services;

public sealed class WpfDiagnosticInfoLauncher(IServiceProvider services) : IDiagnosticInfoLauncher
{
    public async Task ShowAsync()
    {
        var vm = services.GetRequiredService<DiagnosticInfoViewModel>();
        await vm.LoadAsync().ConfigureAwait(true);
        var dlg = new DiagnosticInfoDialog(vm) { Owner = Application.Current?.MainWindow };
        dlg.ShowDialog();
    }
}
