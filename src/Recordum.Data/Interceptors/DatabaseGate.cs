using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Recordum.Data.Interceptors;

/// <summary>A cancello chiuso il database non si apre: il restore lo chiude per scambiare i file (F2c-2), cosi' nessun
/// altro utente (tick dell'auto-backup, query della UI) tiene il file aperto o scrive nel DB sbagliato. Conta le
/// connessioni EF aperte: un permesso all'apertura, restituito alla chiusura, al rilascio o al fallimento, al piu' una volta per
/// connessione. Un'apertura asincrona a cancello chiuso aspetta la riapertura; una sincrona lancia, perche' bloccare il
/// thread della UI congelerebbe anche lo spegnimento.</summary>
public sealed class DatabaseGate : DbConnectionInterceptor
{
    private readonly object _lock = new();
    private readonly ConditionalWeakTable<DbConnection, object> _holders = [];
    private int _open;
    private bool _closed;
    private bool _keptClosed;
    private TaskCompletionSource _reopened = Completed();
    private TaskCompletionSource? _drained;

    /// <summary>Vero dopo <see cref="Quiescence.KeepClosed"/>: il ripristino ha sostituito il database e il cancello
    /// resta chiuso finché il processo vive.</summary>
    public bool IsKeptClosed
    {
        get { lock (_lock) return _keptClosed; }
    }

    /// <summary>Chiude il cancello e aspetta che le connessioni aperte arrivino a zero. Scaduto il tempo riapre e lancia
    /// <see cref="TimeoutException"/>. L'handle riapre al <c>Dispose</c>, salvo <see cref="Quiescence.KeepClosed"/>.</summary>
    public async Task<Quiescence> QuiesceAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        Task drained;
        lock (_lock)
        {
            if (_closed) throw new InvalidOperationException("The database gate is already closed.");
            _closed = true;
            _reopened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_open == 0) _drained.TrySetResult();
            drained = _drained.Task;
        }
        try { await drained.WaitAsync(timeout, ct).ConfigureAwait(false); }
        catch { Reopen(); throw; }
        return new Quiescence(this);
    }

    /// <summary>Riapre il cancello. Dopo un restore riuscito l'app non lo chiama (si chiude, e l'avvio successivo riapre
    /// il database sostituito); serve a chi verifica il risultato nello stesso processo, come i test.</summary>
    public void Reopen()
    {
        lock (_lock)
        {
            _closed = false;
            _keptClosed = false;
            _drained = null;
            _reopened.TrySetResult();
        }
    }

    private void MarkKeptClosed()
    {
        lock (_lock) _keptClosed = true;
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        lock (_lock)
        {
            if (_closed) throw new DatabaseQuiescedException();
            Take(connection);
        }
        return result;
    }

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task reopened;
            lock (_lock)
            {
                if (!_closed) { Take(connection); return result; }
                reopened = _reopened.Task;
            }
            await reopened.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) => Release(connection);

    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
    {
        Release(connection);
        return Task.CompletedTask;
    }

    // Un contesto rilasciato con la connessione ancora aperta la chiude con Dispose, senza passare da ConnectionClosed.
    public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData) => Release(connection);

    public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData)
    {
        Release(connection);
        return Task.CompletedTask;
    }

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData) => Release(connection);

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Release(connection);
        return Task.CompletedTask;
    }

    private void Take(DbConnection connection)
    {
        if (_holders.TryAdd(connection, connection)) _open++;
    }

    private void Release(DbConnection connection)
    {
        lock (_lock)
        {
            if (!_holders.Remove(connection)) return;
            _open--;
            if (_open == 0) _drained?.TrySetResult();
        }
    }

    private static TaskCompletionSource Completed()
    {
        var tcs = new TaskCompletionSource();
        tcs.SetResult();
        return tcs;
    }

    /// <summary>Il cancello chiuso. <c>Dispose</c> lo riapre; dopo <see cref="KeepClosed"/> resta chiuso fino all'uscita
    /// (il restore riuscito: il database e' stato sostituito e l'app si chiude).</summary>
    public sealed class Quiescence(DatabaseGate gate) : IDisposable
    {
        private bool _keep;
        public void KeepClosed()
        {
            _keep = true;
            gate.MarkKeptClosed();
        }
        public void Dispose() { if (!_keep) gate.Reopen(); }
    }
}

/// <summary>Un'apertura sincrona del database mentre il restore lo tiene chiuso.</summary>
public sealed class DatabaseQuiescedException() : InvalidOperationException("The database is closed while a restore replaces it.");
