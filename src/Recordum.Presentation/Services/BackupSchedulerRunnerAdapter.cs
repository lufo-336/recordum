using Recordum.Core.Backup;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.Presentation.Services;

public sealed class BackupSchedulerRunnerAdapter(IBackupScheduler scheduler) : IBackupSchedulerRunner
{
    public Task<BackupRunOutcome> RunNowAsync(CancellationToken ct = default) => scheduler.RunDueAsync(ct);
}
