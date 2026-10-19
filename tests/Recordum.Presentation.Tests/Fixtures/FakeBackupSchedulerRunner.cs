using Recordum.Core.Backup;
using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeBackupSchedulerRunner : IBackupSchedulerRunner
{
    public int RunCount { get; private set; }
    public BackupRunOutcome Outcome { get; set; } = BackupRunOutcome.Created;
    public Task<BackupRunOutcome> RunNowAsync(CancellationToken ct = default) { RunCount++; return Task.FromResult(Outcome); }
}
