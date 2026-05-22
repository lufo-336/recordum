using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Timer;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class TimerRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAsync_loads_all_active_timers_into_the_status_bar()
    {
        var ats = new FakeActiveTimerService();
        ats.Active.Add(new ActiveTimer
        {
            Id = Guid.NewGuid(),
            HabitNodeId = Guid.NewGuid(),
            StartedAtMs = 1_000,
            TimerMode = TimerMode.Stopwatch,
        });
        var statusBar = new TimerStatusBarViewModel(ats, new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(2_000)), new FakeHabitNodeService());
        var sut = new TimerRecoveryService(statusBar);

        await sut.RecoverAsync();

        statusBar.Rows.Should().ContainSingle();
    }
}
