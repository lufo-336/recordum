using FluentAssertions;
using Recordum.Core.Time;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Quick;

public class CompletionStampTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly DayBoundary Boundary = new(Rome, new TimeOnly(5, 0));

    [Fact]
    public void Stamp_today_is_now_and_not_retroactive()
    {
        var now = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero); // 11:00 a Roma: oggi = 27
        var clock = new FakeClock(now, Rome);

        var stamp = CompletionStamp.For(Boundary, clock, new DateOnly(2026, 10, 27));

        stamp.Timestamp.Should().Be(now.UtcDateTime);
        stamp.Ms.Should().Be(now.ToUnixTimeMilliseconds());
        stamp.IsRetroactive.Should().BeFalse();
    }

    [Fact]
    public void Stamp_past_day_is_noon_of_that_day_and_retroactive()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero), Rome);

        // 2026-10-25: giorno del cambio d'ora (CEST → CET); mezzogiorno a Roma = 11:00 UTC.
        var stamp = CompletionStamp.For(Boundary, clock, new DateOnly(2026, 10, 25));

        var expected = new DateTimeOffset(2026, 10, 25, 11, 0, 0, TimeSpan.Zero);
        stamp.Timestamp.Should().Be(expected.UtcDateTime);
        stamp.Ms.Should().Be(expected.ToUnixTimeMilliseconds());
        stamp.IsRetroactive.Should().BeTrue();
    }
}
