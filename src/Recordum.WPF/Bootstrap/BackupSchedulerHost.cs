using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Backup;

namespace Recordum.WPF.Bootstrap;

public sealed class BackupSchedulerHost(IServiceScopeFactory scopes, ILogger<BackupSchedulerHost> log) : IDisposable
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Initialize()
    {
        CleanupOrphanTempDirs();
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(60));
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Fire once at startup, then every tick.
        await TickAsync(ct).ConfigureAwait(false);
        while (await _timer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            await TickAsync(ct).ConfigureAwait(false);
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
