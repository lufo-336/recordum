namespace Recordum.Core.Backup;

public interface IBackupService
{
    Task<BackupResult> CreateAsync(string destinationPath, string passphrase, BackupOptions options, CancellationToken ct = default);
}
