using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Services;
using Recordum.UI.Views;
using Recordum.UI.Views.Startup;

namespace Recordum.UI.Bootstrap;

/// <summary>L'ospite d'avvio della testa desktop: finestra d'attesa, sblocco, errore fatale e <see cref="MainWindow"/>.</summary>
public sealed class DesktopStartupHost(IClassicDesktopStyleApplicationLifetime desktop) : IStartupHost
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop =
        desktop ?? throw new ArgumentNullException(nameof(desktop));
    private RecordumWindow? _waitingWindow;
    private WaitingView? _waitingView;

    public void ShowWaiting(string status)
    {
        if (_waitingWindow is not null && _waitingView is not null)
        {
            _waitingView.Status = status;
            return;
        }

        var view = new WaitingView { Status = status };
        var window = new RecordumWindow
        {
            Title = Strings.AppTitle,
            Width = 360,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = view,
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_waitingWindow, window))
            {
                _waitingWindow = null;
                _waitingView = null;
            }
        };
        _waitingView = view;
        _waitingWindow = window;
        window.Show();
    }

    public async Task<IDbKeyProvider?> UnlockAsync(UnlockViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        CloseWaiting();

        var window = new UnlockWindow(viewModel);
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();

        // Chi arriva prima: lo sblocco riuscito, o l'utente che chiude la finestra.
        var winner = await Task.WhenAny(viewModel.Result, closed.Task).ConfigureAwait(true);
        if (winner != viewModel.Result)
        {
            _desktop.Shutdown();
            return null;
        }

        var provider = await viewModel.Result.ConfigureAwait(true);
        window.Close();
        return provider;
    }

    public void ShowFatal(string message, string? logPath)
    {
        CloseWaiting();
        var window = new FatalErrorWindow(message, logPath);
        window.Closed += (_, _) => _desktop.Shutdown(1);
        window.Show();
    }

    public void ShowMain(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var window = services.GetRequiredService<MainWindow>();
        _desktop.MainWindow = window;
        _desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        // F2a-11: un errore di dominio gestito da CrashLogger si vede, l'app continua.
        RecoverableErrorNotice.Attach(() => _desktop.MainWindow);
        window.Show();
        CloseWaiting();
    }

    private void CloseWaiting()
    {
        if (_waitingWindow is null) return;
        var window = _waitingWindow;
        _waitingWindow = null;
        _waitingView = null;
        window.Close();
    }
}
