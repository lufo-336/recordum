using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.DiagnosticInfo;
using Recordum.WPF.ViewModels.Settings;
using Recordum.WPF.Views.Settings;

namespace Recordum.WPF.Services;

public sealed class WpfSettingsWindowLauncher(IServiceProvider services) : ISettingsWindowLauncher
{
    public async Task ShowAsync(SettingsCategoryId? initialCategory = null)
    {
        var vm = services.GetRequiredService<SettingsWindowViewModel>();
        // Load every real category's content before showing (cheap: a handful of small reads).
        foreach (var category in vm.Categories)
        {
            switch (category.Content)
            {
                case SettingsViewModel s: await s.InitializeAsync().ConfigureAwait(true); break;
                case GeneralSettingsViewModel g: await g.LoadAsync().ConfigureAwait(true); break;
                case BackupSettingsViewModel b: await b.LoadAsync().ConfigureAwait(true); break;
                case DiagnosticInfoViewModel d: await d.LoadAsync().ConfigureAwait(true); break;
            }
        }
        vm.SelectInitial(initialCategory);
        var window = new SettingsWindow(vm) { Owner = Application.Current?.MainWindow };
        window.ShowDialog();
    }
}
