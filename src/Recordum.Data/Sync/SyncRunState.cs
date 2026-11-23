using Recordum.Core.Abstractions.Services;

namespace Recordum.Data.Sync;

/// <summary>Singleton del processo: i pari e i problemi dell'ultimo giro (vuoti dopo un riavvio finché non ne gira uno),
/// e il cancello che tiene un giro solo alla volta fra il pianificatore (thread del pool) e i comandi della UI.</summary>
#pragma warning disable CA1001 // vive quanto il processo; disporre il cancello correrebbe con il finally di un giro sul pool
public sealed class SyncRunState
#pragma warning restore CA1001
{
    private readonly Lock _lock = new();
    private IReadOnlyList<SyncPeerStatus> _peers = [];

    /// <summary>Un giro, un collegamento o uno scollegamento alla volta in tutto il processo.</summary>
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    public IReadOnlyList<SyncPeerStatus> Peers
    {
        get
        {
            lock (_lock) return _peers;
        }
    }

    internal void SetPeers(IReadOnlyList<SyncPeerStatus> peers)
    {
        var copy = peers.ToArray();
        lock (_lock) _peers = copy;
    }
}
