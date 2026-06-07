using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Backup;

namespace Recordum.WPF.Bootstrap;

public sealed class BackupSchedulerHost(
    IServiceScopeFactory scopes,
    ILogger<BackupSchedulerHost> log,
    BackupSchedulerOptions options) : IDisposable
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Initialize()
    {
        if (_loop is not null) return; // idempotent — already started
        CleanupOrphanTempDirs();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            // Short initial delay so we don't race app-startup DB activity (seeded plugin
            // activators, first-run home tree), but a backup due at launch still runs minutes
            // later rather than after a full poll interval.
            await Task.Delay(options.InitialDelay, ct).ConfigureAwait(false);
            await TickAsync(ct).ConfigureAwait(false);

            _timer = new PeriodicTimer(options.Interval);
            while (await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await TickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { } // shutdown
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var sched = scope.ServiceProvider.GetRequiredService<IBackupScheduler>();
            await sched.RunDueAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
#pragma warning disable CA1848
            log.LogWarning(ex, "auto-backup tick failed");
#pragma warning restore CA1848
        }
    }

    private static void CleanupOrphanTempDirs()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            foreach (var d in Directory.EnumerateDirectories(Path.GetTempPath(), "recordum-bk-*"))
            {
                try
                {
                    if (Directory.GetCreationTimeUtc(d) < cutoff)
                        Directory.Delete(d, recursive: true);
                }
                catch { }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
    }
}
