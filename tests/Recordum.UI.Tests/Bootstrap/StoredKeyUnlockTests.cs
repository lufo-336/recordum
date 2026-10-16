using System.Diagnostics;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.UI.Bootstrap;
using Xunit;

namespace Recordum.UI.Tests.Bootstrap;

public class StoredKeyUnlockTests
{
    private sealed class Service(Func<Task<StoredKeyResult>> attempt, Func<bool> canRemember) : IDbUnlockService
    {
        public OrphanReconciliation ReconcileOrphans() => OrphanReconciliation.None;
        public DbUnlockState DetectState() => DbUnlockState.NeedsPassphrase;
        public Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, bool rememberOnDevice, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<StoredKeyResult> TryUnlockWithStoredKeyAsync(CancellationToken ct = default) => attempt();
        public bool CanRememberOnDevice => canRemember();
    }

    private sealed class Disposable : IDbKeyProvider, IDisposable
    {
        public bool Disposed { get; private set; }
        public string ProviderId => "test";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task A_store_that_never_answers_gives_up_within_the_cap_and_says_it_cannot_remember()
    {
        using var never = new ManualResetEventSlim();
        var service = new Service(() => Task.Run(() => { never.Wait(); return new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null); }), () => true);
        var sw = Stopwatch.StartNew();

        var result = await StoredKeyUnlock.RunAsync(service, DbUnlockState.NeedsPassphrase, TimeSpan.FromMilliseconds(200));

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        result.Provider.Should().BeNull();
        result.CanRemember.Should().BeFalse("la sonda potrebbe bloccare ancora: niente casella attiva");
        never.Set();
    }

    [Fact]
    public async Task A_probe_that_blocks_is_inside_the_cap_too()
    {
        using var never = new ManualResetEventSlim();
        var service = new Service(() => Task.FromResult(new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null)), () => { never.Wait(); return true; });

        var result = await StoredKeyUnlock.RunAsync(service, DbUnlockState.NeedsPassphrase, TimeSpan.FromMilliseconds(200));

        result.CanRemember.Should().BeFalse();
        never.Set();
    }

    [Fact]
    public async Task A_late_unlocked_attempt_disposes_its_key()
    {
        var provider = new Disposable();
        var late = new TaskCompletionSource<StoredKeyResult>();
        var service = new Service(() => late.Task, () => true);

        var result = await StoredKeyUnlock.RunAsync(service, DbUnlockState.NeedsPassphrase, TimeSpan.FromMilliseconds(100));
        result.Provider.Should().BeNull();
        late.SetResult(new StoredKeyResult(StoredKeyOutcome.Unlocked, provider));

        SpinWait.SpinUntil(() => provider.Disposed, TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public async Task A_quick_answer_is_passed_through()
    {
        var provider = new Disposable();
        var service = new Service(() => Task.FromResult(new StoredKeyResult(StoredKeyOutcome.Unlocked, provider)), () => true);

        var result = await StoredKeyUnlock.RunAsync(service, DbUnlockState.NeedsPassphrase, TimeSpan.FromSeconds(30));

        result.Provider.Should().BeSameAs(provider);
        result.CanRemember.Should().BeTrue();
        provider.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task A_rejected_key_is_reported_and_creation_never_tries_the_stored_key()
    {
        var calls = 0;
        var service = new Service(() => { calls++; return Task.FromResult(new StoredKeyResult(StoredKeyOutcome.StoredKeyRejected, null)); }, () => true);

        (await StoredKeyUnlock.RunAsync(service, DbUnlockState.NeedsPassphrase, TimeSpan.FromSeconds(30))).Rejected.Should().BeTrue();
        var creation = await StoredKeyUnlock.RunAsync(service, DbUnlockState.NeedsCreation, TimeSpan.FromSeconds(30));

        calls.Should().Be(1);
        creation.Rejected.Should().BeFalse();
        creation.CanRemember.Should().BeTrue();
    }
}
