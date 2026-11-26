using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.ViewModels.Startup;

namespace Recordum.UI.Bootstrap;

/// <summary>Il tentativo con la chiave ricordata, prima della finestra di sblocco. Il portachiavi può bloccare
/// (su Linux libsecret è sincrono e può aspettare un prompt o la sonda D-Bus, ~25 s): tutto gira su un thread del
/// pool e oltre il tetto <c>cap</c> si rinuncia — portachiavi considerato non disponibile, si mostra la passphrase.</summary>
public static class StoredKeyUnlock
{
    public static readonly TimeSpan DefaultCap = TimeSpan.FromSeconds(10);

    /// <param name="Provider">Valorizzato se la chiave ricordata ha aperto il database.</param>
    /// <param name="CanRemember">Il portachiavi c'e' (falso anche se non ha risposto in tempo).</param>
    /// <param name="Rejected">La chiave ricordata non apriva piu' i dati ed e' stata cancellata.</param>
    /// <param name="Reason">Perche' <paramref name="CanRemember"/> e' falso, quando lo e'.</param>
    public sealed record Result(IDbKeyProvider? Provider, bool CanRemember, bool Rejected, RememberUnavailableReason Reason);

    /// <param name="missing">Motivo esposto quando il portachiavi non è disponibile (deciso dalla testa).</param>
    public static async Task<Result> RunAsync(IDbUnlockService service, DbUnlockState state, TimeSpan cap, RememberUnavailableReason missing)
    {
        var abandoned = new CancellationTokenSource();
        var work = Task.Run(async () =>
        {
            var attempt = state == DbUnlockState.NeedsPassphrase
                ? await service.TryUnlockWithStoredKeyAsync(abandoned.Token).ConfigureAwait(false)
                : new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null);
            // Dopo il tentativo: la sonda e' gia' stata fatta e messa in cache. Prima di lui, sarebbe una seconda attesa.
            return new Result(attempt.Provider, service.CanRememberOnDevice, attempt.Outcome == StoredKeyOutcome.StoredKeyRejected, missing);
        });
        using var delayCts = new CancellationTokenSource();
        var winner = await Task.WhenAny(work, Task.Delay(cap, delayCts.Token)).ConfigureAwait(false);
        if (winner == work)
        {
            await delayCts.CancelAsync().ConfigureAwait(false);
            return await work.ConfigureAwait(false);
        }

        // Scaduto: il tentativo abbandonato non deve piu' toccare database ne' portachiavi (v. TryUnlockWithStoredKeyAsync).
        abandoned.Cancel();
        // Scaduto: se il tentativo finisce dopo, la sua chiave non deve restare in memoria.
        _ = work.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result.Provider is IDisposable d) d.Dispose();
        }, TaskScheduler.Default);
        // CanRemember false e' strutturale: senza scritture in questa sessione, un Delete tardivo del tentativo abbandonato
        // non puo' correre contro una Write fresca e cancellare la chiave appena ricordata. Non "correggerlo" a true.
        return new Result(null, CanRemember: false, Rejected: false, RememberUnavailableReason.TimedOut);
    }
}
