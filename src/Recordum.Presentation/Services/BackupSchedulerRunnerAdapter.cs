using Recordum.Core.Backup;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.Presentation.Services;

public sealed class BackupSchedulerRunnerAdapter(IBackupScheduler scheduler) : IBackupSchedulerRunner
{
    public Task RunNowAsync(CancellationToken ct = default) => scheduler.RunDueAsync(ct);
}
