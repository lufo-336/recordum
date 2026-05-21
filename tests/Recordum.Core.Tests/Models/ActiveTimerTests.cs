using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Xunit;

namespace Recordum.Core.Tests.Models;

public class ActiveTimerTests
{
    [Fact]
    public void Defaults_for_a_new_active_timer_are_sane()
    {
        var t = new ActiveTimer { HabitNodeId = Guid.NewGuid(), StartedAtMs = 1_000, TimerMode = TimerMode.Stopwatch };

        t.PausedAtMs.Should().BeNull();
        t.SegmentIndex.Should().BeNull();
        t.AccumulatedPausedMs.Should().Be(0);
    }
}
