using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.UI.Views.Backup;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfBackupDialogLauncher</c>: le due finestre modali sulla principale, con il ViewModel in
/// uno scope per apertura (i servizi di backup sono con scope). Dopo un restore riuscito l'app si chiude, come in WPF:
/// il database e' stato sostituito e l'avvio successivo lo riapre.</summary>
public sealed class AvaloniaBackupDialogLauncher(IServiceProvider services, Func<Window?> ownerResolver) : IBackupDialogLauncher
{
    /// <summary>Test-friendly: le finestre sono create al volo.</summary>
    internal event Action<Window>? WindowCreatedForTests;

    public async Task ShowBackupAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<BackupDialogViewModel>();
        await ShowAsync(new BackupDialog { DataContext = vm });
    }

    public async Task ShowRestoreAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<RestoreDialogViewModel>();
        await ShowAsync(new RestoreDialog { DataContext = vm });
        if (!vm.IsCompleted) return;
        // Il database e' stato sostituito: da qui all'uscita nessun backup automatico deve toccarlo.
        services.GetService<BackupSchedulerHost>()?.Dispose();
        services.GetRequiredService<IApplicationLifetime>().Shutdown();
    }

    private async Task ShowAsync(Window window)
    {
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
