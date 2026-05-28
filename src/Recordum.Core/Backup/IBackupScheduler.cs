namespace Recordum.Core.Backup;

public interface IBackupScheduler
{
    Task RunDueAsync(CancellationToken ct = default);
    DateTime? NextRunUtc { get; }
    bool IsEnabled { get; }
}
