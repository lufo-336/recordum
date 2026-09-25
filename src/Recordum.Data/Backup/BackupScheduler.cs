
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

public sealed partial class BackupScheduler(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IBackupService backupService,
    IBackupStoragePaths paths,
    IAutoBackupKeyStore keys,
    ILogger<BackupScheduler> logger,
    IClock clock) : IBackupScheduler
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Auto-backup switched off: no readable passphrase in the key store")]
    private partial void LogSelfDisabled();

    public bool IsEnabled { get; private set; } = true;
    public DateTime? NextRunUtc { get; private set; }

    public async Task RunDueAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var settings = await db.AppSettings.AsTracking()
            .Where(x => x.Key.StartsWith("backup.auto."))
            .ToDictionaryAsync(x => x.Key, ct).ConfigureAwait(false);

        var enabled = settings.TryGetValue("backup.auto.enabled", out var enabledSetting)
            && enabledSetting.Value == "true";
        IsEnabled = enabled;
        if (!enabled) return;

        // Senza una passphrase leggibile l'auto-backup non può girare: si spegne, e ora lo scrive nel log. Prima si
        // spegneva in silenzio, e l'utente continuava a credere che i suoi dati fossero salvati (RV-5).
        var passphrase = keys.ReadKey();
        if (passphrase is null)
        {
            if (settings.TryGetValue("backup.auto.enabled", out var s))
            {
                s.Value = "false";
                s.UpdatedAt = clock.UtcNow.UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            IsEnabled = false;
            LogSelfDisabled();
            return;
        }

        var frequencyDays = settings.TryGetValue("backup.auto.frequency_days", out var freqSetting)
            && int.TryParse(freqSetting.Value, out var d) ? d : 7;

        var lastRunStr = settings.TryGetValue("backup.auto.last_run_utc", out var lastRunSetting)
            ? lastRunSetting.Value : string.Empty;
        var lastRun = string.IsNullOrWhiteSpace(lastRunStr) ? DateTime.MinValue
            : DateTime.Parse(lastRunStr, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var nextRun = lastRun.AddDays(frequencyDays);
        NextRunUtc = nextRun;

        if (clock.UtcNow.UtcDateTime < nextRun) return;

        var folder = settings.TryGetValue("backup.auto.path", out var pathSetting)
            && !string.IsNullOrWhiteSpace(pathSetting.Value)
            ? pathSetting.Value
            : paths.BackupsDirectory;
        Directory.CreateDirectory(folder);

        var dest = Path.Combine(folder, $"auto-{clock.UtcNow.UtcDateTime:yyyyMMdd-HHmmss}.recbak");
        await backupService.CreateAsync(dest, passphrase, new BackupOptions(BackupKind.Auto), ct).ConfigureAwait(false);

        if (settings.TryGetValue("backup.auto.last_run_utc", out var lastRunEntry))
        {
            lastRunEntry.Value = clock.UtcNow.UtcDateTime.ToString("O");
            lastRunEntry.UpdatedAt = clock.UtcNow.UtcDateTime;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
