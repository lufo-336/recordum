using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.UI.Views.Settings;

namespace Recordum.UI.Services;

/// <summary>Apre la finestra Impostazioni ridotta. C'e' una sola pagina, quindi la categoria richiesta si ignora.
/// Il ViewModel vive in uno scope per apertura (i servizi di backup sono con scope), come i dialog di backup.</summary>
public sealed class AvaloniaSettingsWindowLauncher(IServiceProvider services, Func<Window?> ownerResolver) : ISettingsWindowLauncher
{
    internal event Action<Window>? WindowCreatedForTests;

    public async Task ShowAsync(SettingsCategoryId? initialCategory = null)
    {
        await using var scope = services.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<DataAndBackupPageViewModel>();
        await vm.LoadAsync().ConfigureAwait(true);
        var window = new SettingsWindow { DataContext = vm };
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
