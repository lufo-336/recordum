using Recordum.Core.Abstractions.Security;

namespace Recordum.UI.Bootstrap;

/// <summary>Il tentativo con la chiave ricordata, prima della finestra di sblocco. libsecret e' sincrono e puo' aspettare un
/// prompt di sblocco della collezione (o la sonda D-Bus, ~25 s): tutto gira su un thread del pool e oltre
/// <paramref name="cap"/> si rinuncia — portachiavi considerato non disponibile, si mostra la passphrase.</summary>
public static class StoredKeyUnlock
{
    public static readonly TimeSpan DefaultCap = TimeSpan.FromSeconds(10);

    /// <param name="Provider">Valorizzato se la chiave ricordata ha aperto il database.</param>
    /// <param name="CanRemember">Il portachiavi c'e' (falso anche se non ha risposto in tempo).</param>
    /// <param name="Rejected">La chiave ricordata non apriva piu' i dati ed e' stata cancellata.</param>
    public sealed record Result(IDbKeyProvider? Provider, bool CanRemember, bool Rejected);

    public static async Task<Result> RunAsync(IDbUnlockService service, DbUnlockState state, TimeSpan cap)
    {
        var work = Task.Run(async () =>
        {
            var attempt = state == DbUnlockState.NeedsPassphrase
                ? await service.TryUnlockWithStoredKeyAsync().ConfigureAwait(false)
                : new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null);
            // Dopo il tentativo: la sonda e' gia' stata fatta e messa in cache. Prima di lui, sarebbe una seconda attesa.
            return new Result(attempt.Provider, service.CanRememberOnDevice, attempt.Outcome == StoredKeyOutcome.StoredKeyRejected);
        });
        using var delayCts = new CancellationTokenSource();
        var winner = await Task.WhenAny(work, Task.Delay(cap, delayCts.Token)).ConfigureAwait(false);
        if (winner == work)
        {
            await delayCts.CancelAsync().ConfigureAwait(false);
            return await work.ConfigureAwait(false);
        }

        // Scaduto: se il tentativo finisce dopo, la sua chiave non deve restare in memoria.
        _ = work.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result.Provider is IDisposable d) d.Dispose();
        }, TaskScheduler.Default);
        return new Result(null, CanRemember: false, Rejected: false);
    }
}
