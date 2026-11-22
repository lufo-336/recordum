using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.UI.Views.Settings;

namespace Recordum.UI.Services;

/// <summary>Apre la finestra Impostazioni (Aspetto, Dati e backup) sulla pagina della categoria richiesta.
/// I ViewModel vivono in uno scope per apertura (i servizi di backup sono con scope), come i dialog di backup.</summary>
public sealed class AvaloniaSettingsWindowLauncher(IServiceProvider services, Func<Window?> ownerResolver) : ISettingsWindowLauncher
{
    internal event Action<Window>? WindowCreatedForTests;

    public async Task ShowAsync(SettingsCategoryId? initialCategory = null)
    {
        await using var scope = services.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<DataAndBackupPageViewModel>();
        await vm.LoadAsync().ConfigureAwait(true);
        var appearance = scope.ServiceProvider.GetRequiredService<SettingsViewModel>();
        await appearance.InitializeAsync().ConfigureAwait(true);
        var sync = scope.ServiceProvider.GetRequiredService<SyncSettingsViewModel>();
        await sync.LoadAsync().ConfigureAwait(true);
        var window = new SettingsWindow { DataContext = vm, Appearance = appearance, Sync = sync };
        window.ShowPage(initialCategory);
        WindowCreatedForTests?.Invoke(window);
        if (ownerResolver() is { } owner)
            await window.ShowDialog(owner);
        else
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            window.Show();
            await closed.Task;
        }
    }
}
