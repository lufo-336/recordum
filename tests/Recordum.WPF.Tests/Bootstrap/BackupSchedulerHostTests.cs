using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Backup;
using Recordum.Presentation.Bootstrap;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

public class BackupSchedulerHostTests
{
    private sealed class FakeScheduler : IBackupScheduler
    {
        private int _runDueCount;
        public int RunDueCount => Volatile.Read(ref _runDueCount);
        public Task RunDueAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _runDueCount);
            return Task.CompletedTask;
        }
        public DateTime? NextRunUtc => null;
        public bool IsEnabled => true;
    }

    [Fact]
    public async Task Initialize_runs_first_due_check_after_initial_delay()
    {
        var sched = new FakeScheduler();
        var services = new ServiceCollection();
        services.AddScoped<IBackupScheduler>(_ => sched);
        using var provider = services.BuildServiceProvider();
        var options = new BackupSchedulerOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(50),
            Interval = TimeSpan.FromMilliseconds(50),
        };
        using var host = new BackupSchedulerHost(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackupSchedulerHost>.Instance,
            options);

        host.Initialize();
        await Task.Delay(400);

        sched.RunDueCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddScoped<IBackupScheduler>(_ => new FakeScheduler());
        using var provider = services.BuildServiceProvider();
        using var host = new BackupSchedulerHost(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackupSchedulerHost>.Instance,
            new BackupSchedulerOptions { InitialDelay = TimeSpan.FromMinutes(5), Interval = TimeSpan.FromMinutes(5) });

        host.Initialize();
        var second = () => host.Initialize();

        second.Should().NotThrow();
    }
}
