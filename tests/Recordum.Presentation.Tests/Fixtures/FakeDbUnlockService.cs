using Recordum.Core.Abstractions.Security;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>Un servizio di sblocco a risposte programmate. <see cref="Gate"/>, se impostato, tiene la
/// chiamata in volo finché il test non lo completa: è ciò che rende osservabile <c>IsBusy</c> a metà.</summary>
public sealed class FakeDbUnlockService : IDbUnlockService
{
    private readonly Queue<UnlockResult> _results = new();

    public DbUnlockState State { get; set; } = DbUnlockState.NeedsPassphrase;
    public List<(DbUnlockState State, string Passphrase)> Calls { get; } = [];
    public TaskCompletionSource? Gate { get; set; }
    public Exception? Throw { get; set; }

    public void Enqueue(UnlockOutcome outcome, IDbKeyProvider? provider = null) =>
        _results.Enqueue(new UnlockResult(outcome, provider));

    public OrphanReconciliation ReconcileOrphans() => OrphanReconciliation.None;

    public DbUnlockState DetectState() => State;

    public async Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, CancellationToken ct = default)
    {
        Calls.Add((state, passphrase));
        if (Gate is not null) await Gate.Task;
        if (Throw is not null) throw Throw;
        return _results.Dequeue();
    }
}
