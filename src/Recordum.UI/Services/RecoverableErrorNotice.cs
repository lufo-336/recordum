using Avalonia.Controls;
using Recordum.Presentation.Resources;
using Recordum.UI.Diagnostics;
using Recordum.UI.Views.Startup;

namespace Recordum.UI.Services;

/// <summary>Rende visibile un errore di dominio che <see cref="CrashLogger"/> ha gestito (F2a-11): un avviso
/// con solo "OK", uno alla volta — una raffica di errori dallo stesso gesto non impila finestre.</summary>
public sealed class RecoverableErrorNotice : IDisposable
{
    private readonly Func<Window?> _owner;
    private ConfirmationWindow? _open;

    /// <summary>Solo per i test: la finestra appena aperta.</summary>
    internal event Action<ConfirmationWindow>? NoticeShownForTests;

    private RecoverableErrorNotice(Func<Window?> owner) => _owner = owner;

    public static RecoverableErrorNotice Attach(Func<Window?> owner)
    {
        var notice = new RecoverableErrorNotice(owner);
        CrashLogger.RecoverableError += notice.OnRecoverableError;
        return notice;
    }

    private void OnRecoverableError(Exception ex)
    {
        if (_open is not null) return;
        var window = new ConfirmationWindow(Strings.Error_Recoverable_Title, Strings.Error_Recoverable_Message, okOnly: true);
        window.YesButton.Click += (_, _) => window.Close();
        window.Closed += (_, _) => _open = null;
        _open = window;
        NoticeShownForTests?.Invoke(window);
        if (_owner() is { } owner) _ = window.ShowDialog(owner);
        else window.Show();
    }

    public void Dispose() => CrashLogger.RecoverableError -= OnRecoverableError;
}
