using System.Globalization;
using FluentAssertions;
using Recordum.Core.Formatting;
using Xunit;

namespace Recordum.Core.Tests.Formatting;

public class SecondsDisplayTests
{
    // Expected values are built with CurrentCulture (not a hardcoded "7.5" literal) so this test
    // stays green on an it-IT machine where the decimal separator is a comma, matching InUnit's
    // own use of CultureInfo.CurrentCulture (house style, see FormatMag/DaySummaryFormatter).
    [Theory]
    [InlineData(27000, "hours", 7.5)]
    [InlineData(5400, "minutes", 90.0)]
    [InlineData(45, "seconds", 45.0)]
    public void InUnit_converts_to_the_metric_unit(int secs, string unit, double expectedValue)
    {
        var expected = ((decimal)expectedValue).ToString("0.##", CultureInfo.CurrentCulture) + " " + unit;
        SecondsDisplay.InUnit(secs, unit).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("l")]
    public void InUnit_returns_null_for_non_time_units(string? unit)
        => SecondsDisplay.InUnit(90, unit).Should().BeNull();

    [Fact]
    public void ConvertToUnit_matches_the_display_conversion()
        => SecondsDisplay.ConvertToUnit(27000, "hours").Should().Be(7.5m);
}
