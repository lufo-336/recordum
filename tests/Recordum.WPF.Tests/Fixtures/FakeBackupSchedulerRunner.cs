using Recordum.WPF.ViewModels.Backup;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeBackupSchedulerRunner : IBackupSchedulerRunner
{
    public int RunCount { get; private set; }
    public Task RunNowAsync(CancellationToken ct = default) { RunCount++; return Task.CompletedTask; }
}
