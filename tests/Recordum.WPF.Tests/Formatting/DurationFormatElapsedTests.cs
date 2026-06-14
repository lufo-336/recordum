using FluentAssertions;
using Recordum.WPF.Formatting;
using Xunit;

namespace Recordum.WPF.Tests.Formatting;

public class DurationFormatElapsedTests
{
    [Fact]
    public void Years_and_days() =>
        DurationFormat.FormatElapsed(TimeSpan.FromDays(2 * 365 + 41) + TimeSpan.FromHours(6))
            .Should().Be("2a 41g");

    [Fact]
    public void Days_and_hours() =>
        DurationFormat.FormatElapsed(TimeSpan.FromDays(41) + TimeSpan.FromHours(6))
            .Should().Be("41g 6h");

    [Fact]
    public void Hours_and_minutes() =>
        DurationFormat.FormatElapsed(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(12))
            .Should().Be("6h 12m");

    [Fact]
    public void Minutes_only() =>
        DurationFormat.FormatElapsed(TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(30))
            .Should().Be("12m");

    [Fact]
    public void Under_a_minute_is_just_now() =>
        DurationFormat.FormatElapsed(TimeSpan.FromSeconds(20)).Should().Be("appena ora");

    [Fact]
    public void Negative_is_just_now() =>
        DurationFormat.FormatElapsed(TimeSpan.FromSeconds(-5)).Should().Be("appena ora");
}
