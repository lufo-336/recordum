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
    public void UtcNowMs_matches_UtcNow_within_two_ms()
    {
        var sut = new SystemClock();

        var snapshotUtc = sut.UtcNow.ToUnixTimeMilliseconds();
        var snapshotMs = sut.UtcNowMs;

        Assert.InRange(snapshotMs - snapshotUtc, -2, 2);
    }

    [Fact]
    public void LocalTimeZone_returns_TimeZoneInfo_Local()
    {
        var clock = new SystemClock();
        Assert.Equal(TimeZoneInfo.Local.Id, clock.LocalTimeZone.Id);
    }
}
