using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.ViewModels.Startup;

namespace Recordum.UI.Bootstrap;

/// <summary>L'ospite delle viste d'avvio e della schermata principale (computer: finestre desktop; telefono: viste nella cornice).</summary>
public interface IStartupHost
{
    /// <summary>Mostra o aggiorna lo stato dell'attesa durante l'avvio.</summary>
    void ShowWaiting(string status);

    /// <summary>Mostra lo sblocco con passphrase; restituisce <c>null</c> se l'utente ha chiuso senza sbloccare.</summary>
    Task<IDbKeyProvider?> UnlockAsync(UnlockViewModel viewModel);

    /// <summary>Mostra un errore fatale d'avvio insieme all'eventuale percorso del log.</summary>
    void ShowFatal(string message, string? logPath);

    /// <summary>Mostra la schermata principale dell'app usando il contenitore costruito.</summary>
    void ShowMain(IServiceProvider services);
}
