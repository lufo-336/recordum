using Recordum.Core.Abstractions;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Timer;
using Xunit;

namespace Recordum.Presentation.Tests.Services;

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
        var statusBar = new TimerStatusBarViewModel(ats, new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(2_000)), new FakeHabitNodeService(), new FakeEventBus(), NullTickSourceFactory.Instance);
        var sut = new TimerRecoveryService(statusBar);

        await sut.RecoverAsync();

        statusBar.Rows.Should().ContainSingle();
    }
}
