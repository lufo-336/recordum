#pragma warning disable CA1416   // ProtectedData is Windows-only

using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;

namespace Recordum.Data.Backup;

[SupportedOSPlatform("windows")]
public sealed class BackupScheduler(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IBackupService backupService,
    IBackupStoragePaths paths,
    IClock clock) : IBackupScheduler
{
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

        // Self-disable if the auto-backup key file is missing
        if (!File.Exists(paths.AutoBackupKeyPath))
        {
            if (settings.TryGetValue("backup.auto.enabled", out var s))
            {
                s.Value = "false";
                s.UpdatedAt = clock.UtcNow.UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            IsEnabled = false;
            return;
        }

        string passphrase;
        try
        {
            var wrapped = await File.ReadAllBytesAsync(paths.AutoBackupKeyPath, ct).ConfigureAwait(false);
            var raw = ProtectedData.Unprotect(wrapped, optionalEntropy: null, DataProtectionScope.CurrentUser);
            passphrase = Encoding.UTF8.GetString(raw);
            CryptographicOperations.ZeroMemory(raw);
        }
        catch
        {
            // DPAPI failure → self-disable
            if (settings.TryGetValue("backup.auto.enabled", out var s))
            {
                s.Value = "false";
                s.UpdatedAt = clock.UtcNow.UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            IsEnabled = false;
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
