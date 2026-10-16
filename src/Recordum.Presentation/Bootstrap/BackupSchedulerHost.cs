using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;
using Recordum.Data.Backup;

namespace Recordum.Presentation.Bootstrap;

public sealed class BackupSchedulerHost(
    IServiceScopeFactory scopes,
    ILogger<BackupSchedulerHost> log,
    BackupSchedulerOptions options,
    IBackupStoragePaths paths,
    IClock clock) : IDisposable
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Initialize()
    {
        if (_loop is not null) return; // idempotent — already started
        CleanupOrphanTempDirs();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token; // read now: a Dispose racing the pool thread would make a later _cts.Token throw
        _loop = Task.Run(() => LoopAsync(ct));
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

    /// <summary>I resti di un backup o di un restore interrotti sono dump del database in chiaro: si distruggono
    /// come il backup distrugge i propri (CR4). Stanno nella cartella dati; la temp di sistema si guarda ancora per i
    /// resti delle versioni precedenti, che scrivevano lì — su Linux leggibili da ogni utente locale (RV-4).</summary>
    private void CleanupOrphanTempDirs()
    {
        var cutoff = clock.UtcNow.UtcDateTime.AddHours(-1);
        WipeOlderThan(paths.TempDirectory, "*", cutoff);
        WipeOlderThan(Path.GetTempPath(), "recordum-bk-*", cutoff);
        WipeOlderThan(Path.GetTempPath(), "recordum-rs-*", cutoff);
    }

    private void WipeOlderThan(string dir, string pattern, DateTime cutoff)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(dir, pattern))
        {
            try
            {
                if (Directory.Exists(entry))
                {
                    if (Directory.GetCreationTimeUtc(entry) < cutoff) SecureWipe.DeleteDirectory(entry);
                }
                else if (File.GetCreationTimeUtc(entry) < cutoff)
                {
                    SecureWipe.DeleteFile(entry);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
#pragma warning disable CA1848
                log.LogWarning(ex, "could not wipe the leftover {Entry}", entry);
#pragma warning restore CA1848
            }
        }
    }

    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _cts, null);   // idempotente: il restore la ferma, poi la chiusura la ferma di nuovo
        cts?.Cancel();
        _timer?.Dispose();
        cts?.Dispose();
    }
}
