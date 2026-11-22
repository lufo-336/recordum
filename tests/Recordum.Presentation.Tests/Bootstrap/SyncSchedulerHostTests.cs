using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Tests.Abstractions;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Tests.Fixtures;

namespace Recordum.Presentation.Tests.Bootstrap;

public class SyncSchedulerHostTests
{
    private sealed class FakeSyncService : ISyncService
    {
        private int _runCount;
        private int _activeRuns;
        private int _maxConcurrentRuns;
        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;

        public int RunCount => Volatile.Read(ref _runCount);
        public int MaxConcurrentRuns => Volatile.Read(ref _maxConcurrentRuns);
        public Task Entered => _entered?.Task ?? Task.CompletedTask;

        public void BlockNextRun()
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release() => _release?.TrySetResult();

        public async Task<SyncRunResult> RunAsync(CancellationToken ct)
        {
            var active = Interlocked.Increment(ref _activeRuns);
            UpdateMax(active);
            Interlocked.Increment(ref _runCount);

            var entered = _entered; // non si azzerano: Release() deve ritrovare il gate dopo che il giro e' entrato
            var release = _release;
            try
            {
                entered?.TrySetResult();
                if (release is not null)
                    await release.Task.WaitAsync(ct).ConfigureAwait(false);
                return new SyncRunResult(Ran: true, OwnFileWritten: true, Peers: []);
            }
            finally
            {
                Interlocked.Decrement(ref _activeRuns);
            }
        }

        private void UpdateMax(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrentRuns);
                if (active <= current) return;
                if (Interlocked.CompareExchange(ref _maxConcurrentRuns, active, current) == current) return;
            }
        }

        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new SyncStatus(null, [], 0, 0));

        public Task SetPassphraseAsync(string passphrase, CancellationToken ct) => Task.CompletedTask;

        public Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct) =>
            Task.FromResult(SyncJoinState.Joined);

        public Task JoinAsync(SyncJoinChoice choice, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct) =>
            Task.CompletedTask;

        public Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private static (ServiceProvider Provider, SyncSchedulerHost Host, FakeSyncService Sync, FakeAppSettingService Settings, FakeTickSourceFactory Ticks, FakeClock Clock) CreateSut()
    {
        var sync = new FakeSyncService();
        var settings = new FakeAppSettingService();
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddScoped<ISyncService>(_ => sync);
        services.AddScoped<IAppSettingService>(_ => settings);
        var provider = services.BuildServiceProvider();

        var host = new SyncSchedulerHost(
            provider.GetRequiredService<IServiceScopeFactory>(),
            ticks,
            clock,
            NullLogger<SyncSchedulerHost>.Instance);

        return (provider, host, sync, settings, ticks, clock);
    }

    [Fact]
    public async Task RunsOnStart()
    {
        var (provider, host, sync, _, ticks, _) = CreateSut();
        using var _ = provider;
        using var __ = host;

        await host.StartAsync(CancellationToken.None);

        sync.RunCount.Should().Be(1);
        ticks.RunningCount.Should().Be(1);
    }

    [Fact]
    public async Task RunsEveryFiveMinutes()
    {
        var (provider, host, sync, _, ticks, _) = CreateSut();
        using var _ = provider;
        using var __ = host;

        await host.StartAsync(CancellationToken.None);
        ticks.RequestedIntervals.Should().ContainSingle().Which.Should().Be(TimeSpan.FromMinutes(5));

        ticks.TickAll();
        await host.CurrentRun;
        sync.RunCount.Should().Be(2);

        ticks.TickAll();
        await host.CurrentRun;
        sync.RunCount.Should().Be(3);
    }

    [Fact]
    public async Task NudgeThrottledTo30Seconds()
    {
        var (provider, host, sync, _, _, clock) = CreateSut();
        using var _ = provider;
        using var __ = host;

        await host.StartAsync(CancellationToken.None);
        sync.RunCount.Should().Be(1);

        host.Nudge();
        await host.CurrentRun;
        sync.RunCount.Should().Be(2);

        clock.Advance(TimeSpan.FromSeconds(10));
        host.Nudge();
        await host.CurrentRun;
        sync.RunCount.Should().Be(2);

        clock.Advance(TimeSpan.FromSeconds(19));
        host.Nudge();
        await host.CurrentRun;
        sync.RunCount.Should().Be(2);

        clock.Advance(TimeSpan.FromSeconds(1));
        host.Nudge();
        await host.CurrentRun;
        sync.RunCount.Should().Be(3);
    }

    [Fact]
    public async Task NeverOverlaps()
    {
        var (provider, host, sync, _, ticks, clock) = CreateSut();
        using var _ = provider;
        using var __ = host;

        await host.StartAsync(CancellationToken.None);
        sync.RunCount.Should().Be(1);

        sync.BlockNextRun();
        ticks.TickAll();
        await sync.Entered;

        // Mentre un giro e' in corso, sia un nuovo battito sia un Nudge si ignorano.
        ticks.TickAll();
        clock.Advance(TimeSpan.FromMinutes(1));
        host.Nudge();

        sync.Release();
        await host.CurrentRun;

        sync.MaxConcurrentRuns.Should().Be(1);
        sync.RunCount.Should().Be(2);
    }

    [Fact]
    public async Task DisabledRunsOnlyOnDemand()
    {
        var (provider, host, sync, settings, ticks, clock) = CreateSut();
        using var _ = provider;
        using var __ = host;

        settings.Store[SyncSchedulerHost.EnabledSettingKey] = "false";

        await host.StartAsync(CancellationToken.None);
        ticks.TickAll();
        await host.CurrentRun;
        clock.Advance(TimeSpan.FromMinutes(1));
        host.Nudge();
        await host.CurrentRun;

        sync.RunCount.Should().Be(0);

        // "Unisci ora" chiama direttamente ISyncService.RunAsync senza passare dal pianificatore.
        await sync.RunAsync(CancellationToken.None);
        sync.RunCount.Should().Be(1);
    }

    [Fact]
    public async Task FlushWritesOnExit()
    {
        var (provider, host, sync, _, ticks, _) = CreateSut();
        using var _ = provider;
        using var __ = host;

        await host.StartAsync(CancellationToken.None);
        sync.RunCount.Should().Be(1);

        // Se un giro e' gia' in corso alla chiusura, FlushAsync lo attende invece di sovrapporsi e poi fa il giro finale.
        sync.BlockNextRun();
        ticks.TickAll();
        await sync.Entered;

        var flush = host.FlushAsync(CancellationToken.None);
        flush.IsCompleted.Should().BeFalse();
        sync.MaxConcurrentRuns.Should().Be(1);

        sync.Release();
        await flush;

        sync.MaxConcurrentRuns.Should().Be(1);
        sync.RunCount.Should().Be(3);
    }
}
