using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Data.Interceptors;

namespace Recordum.Presentation.Bootstrap;

public sealed class SyncSchedulerHost(
    IServiceScopeFactory scopes,
    ITickSourceFactory ticks,
    IClock clock,
    ILogger<SyncSchedulerHost>? log = null,
    DatabaseGate? dbGate = null) : IDisposable
{
    public const string EnabledSettingKey = "sync.enabled";
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan NudgeCooldown = TimeSpan.FromSeconds(30);

#pragma warning disable CA2213 // Non usa AvailableWaitHandle; non si dispone per non correre con il finally di un giro sul thread pool.
    private readonly SemaphoreSlim _gate = new(1, 1);
#pragma warning restore CA2213
    private readonly Lock _nudgeLock = new();
    private ITickSource? _tick;
    private CancellationTokenSource? _cts;
    private DateTimeOffset? _lastNudgeAt;
    private Task _currentRun = Task.CompletedTask;
    private int _disposed;

    /// <summary>Il giro in corso (o l'ultimo concluso), per attendere il lavoro lanciato su <c>Task.Run</c>.</summary>
    public Task CurrentRun => Volatile.Read(ref _currentRun);

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        if (_tick is null)
        {
            _cts = new CancellationTokenSource();
            _tick = ticks.Create(Interval);
            _tick.Tick += OnTick;
            _tick.Start();
        }

        if (!_gate.Wait(0, CancellationToken.None)) return;
        await RunLockedOnPoolAsync(ct).ConfigureAwait(false);
    }

    public void Nudge()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        lock (_nudgeLock)
        {
            var now = clock.UtcNow;
            if (_lastNudgeAt is { } last && now - last < NudgeCooldown)
                return;
            if (!TryStartBackgroundRun())
                return;
            _lastNudgeAt = now;
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _disposed) != 0 || _tick is null) return; // mai partito: niente da scrivere
        _tick.Stop();
        // Dopo un ripristino il cancello resta chiuso fino all'uscita: aprire il DB bloccherebbe fino al timeout.
        if (dbGate?.IsKeptClosed == true) return;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        if (dbGate?.IsKeptClosed == true)
        {
            _gate.Release();
            return;
        }
        await RunLockedOnPoolAsync(ct).ConfigureAwait(false);
    }

    private void OnTick(object? sender, EventArgs e) => TryStartBackgroundRun();

    private bool TryStartBackgroundRun()
    {
        if (Volatile.Read(ref _disposed) != 0) return false;
        if (!_gate.Wait(0, CancellationToken.None)) return false;

        _ = RunLockedOnPoolAsync(_cts?.Token ?? CancellationToken.None);
        return true;
    }

    private Task RunLockedOnPoolAsync(CancellationToken ct)
    {
        var run = Task.Run(async () =>
        {
            try
            {
                await RunCycleCoreAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }, CancellationToken.None);
        Volatile.Write(ref _currentRun, run);
        return run;
    }

    private async Task RunCycleCoreAsync(CancellationToken ct)
    {
        if (dbGate?.IsKeptClosed == true) return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetService<IAppSettingService>();
            if (settings is not null && !IsSyncEnabled(await settings.GetAsync(EnabledSettingKey, ct).ConfigureAwait(false)))
                return;

            var sync = scope.ServiceProvider.GetService<ISyncService>();
            if (sync is null)
            {
#pragma warning disable CA1848
                log?.LogWarning("auto-sync cycle skipped: ISyncService is not registered");
#pragma warning restore CA1848
                return;
            }

            await sync.RunAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
#pragma warning disable CA1848
            log?.LogWarning(ex, "auto-sync cycle failed");
#pragma warning restore CA1848
        }
    }

    private static bool IsSyncEnabled(string? raw) =>
        !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(raw, "0", StringComparison.Ordinal);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_tick is not null)
        {
            _tick.Tick -= OnTick;
            _tick.Stop();
            _tick.Dispose();
        }
        var cts = Interlocked.Exchange(ref _cts, null);
        cts?.Cancel();
        cts?.Dispose();
    }
}
