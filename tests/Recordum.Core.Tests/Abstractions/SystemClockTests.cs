using Recordum.Core.Abstractions;
using Xunit;

namespace Recordum.Core.Tests.Abstractions;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_current_utc_time_within_one_second()
    {
        var sut = new SystemClock();

        var before = DateTimeOffset.UtcNow;
        var now = sut.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(now.UtcTicks, before.UtcTicks, after.UtcTicks);
    }

    [Fact]
    public void UtcNowMs_is_the_same_clock_as_UtcNow()
    {
        var sut = new SystemClock();

        // Bracket: UtcNowMs must fall between two UtcNow reads, whatever the scheduler does in between.
        var before = sut.UtcNow.ToUnixTimeMilliseconds();
        var ms = sut.UtcNowMs;
        var after = sut.UtcNow.ToUnixTimeMilliseconds();

        Assert.InRange(ms, before, after);
    }

    [Fact]
    public void LocalTimeZone_returns_TimeZoneInfo_Local()
    {
        var clock = new SystemClock();
        Assert.Equal(TimeZoneInfo.Local.Id, clock.LocalTimeZone.Id);
    }
}
