using Recordum.Presentation.ViewModels.Backup;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeBackupSchedulerRunner : IBackupSchedulerRunner
{
    public int RunCount { get; private set; }
    public Task RunNowAsync(CancellationToken ct = default) { RunCount++; return Task.CompletedTask; }
}
