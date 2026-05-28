using Recordum.Core.Backup;
using Recordum.WPF.ViewModels.Backup;

namespace Recordum.WPF.Services;

public sealed class BackupSchedulerRunnerAdapter(IBackupScheduler scheduler) : IBackupSchedulerRunner
{
    public Task RunNowAsync(CancellationToken ct = default) => scheduler.RunDueAsync(ct);
}
