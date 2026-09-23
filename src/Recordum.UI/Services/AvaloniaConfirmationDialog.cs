using Avalonia.Controls;
using Recordum.Presentation.Services;
using Recordum.UI.Views.Startup;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>Recordum.WPF.Services.MessageBoxConfirmationDialog</c>
/// (<c>MessageBox.Show(message, title, YesNo, Question)</c>). Avalonia non ha un MessageBox nativo,
/// quindi qui è una vera piccola <see cref="Window"/> sì/no (<see cref="ConfirmationWindow"/>), non
/// un Null che risponderebbe sempre <c>true</c> — Home usa questa dialog per confermare Elimina/Purge,
/// e un Null "true" cancellerebbe dati senza chiedere.
/// <para>Il resolver della finestra proprietaria è iniettato (non letto da
/// <c>Application.Current.ApplicationLifetime</c> al proprio interno) per lo stesso motivo di
/// <see cref="AvaloniaApplicationLifetime"/>: quella proprietà non esiste ancora quando un test
/// headless costruisce l'oggetto. La registrazione reale (Task 11) passa il resolver della
/// MainWindow dell'app.</para></summary>
public sealed class AvaloniaConfirmationDialog(Func<Window?> ownerResolver) : IConfirmationDialog
{
    /// <summary>Test-friendly: nessun altro modo di ottenere un riferimento alla finestra che
    /// <see cref="ConfirmAsync"/> crea al volo (stesso pattern di visibilità di
    /// <c>ThemeService</c> in WPF — internal + <c>InternalsVisibleTo</c> verso il progetto di test,
    /// non una API pubblica che l'app reale non userebbe mai).</summary>
    internal event Action<ConfirmationWindow>? DialogCreatedForTests;

    public Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmationWindow(title, message);
        var owner = ownerResolver();
        DialogCreatedForTests?.Invoke(dialog);

        // ShowDialog<bool>(owner) ha un canale nativo per il risultato (Close(bool)); senza owner
        // Show() non ce l'ha, quindi il risultato passa da un TaskCompletionSource risolto PRIMA di
        // chiudere — non da un secondo handler di Click che legge una variabile dopo Close(), che
        // corre contro l'evento Closed (misurato: Closed scatta DENTRO Close(), quindi un handler di
        // Click iscritto dopo quello che chiama Close() non fa più in tempo a girare prima che il
        // valore serva).
        if (owner is not null)
        {
            dialog.YesButton.Click += (_, _) => dialog.Close(true);
            dialog.NoButton.Click += (_, _) => dialog.Close(false);
            return dialog.ShowDialog<bool>(owner);
        }

        return ShowWithoutOwnerAsync(dialog);
    }

    private static Task<bool> ShowWithoutOwnerAsync(ConfirmationWindow dialog)
    {
        var tcs = new TaskCompletionSource<bool>();
        dialog.YesButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        dialog.NoButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        // Se l'utente chiude la finestra via X / Alt+F4 senza premere un bottone, di default rispondi false.
        // TrySetResult(true) dal Yes button (eseguito prima di Close()) vince comunque.
        dialog.Closed += (_, _) => tcs.TrySetResult(false);
        dialog.Show();
        return tcs.Task;
    }
}
