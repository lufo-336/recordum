using FluentAssertions;
using Recordum.Core.Formatting;
using Xunit;

namespace Recordum.Core.Tests.Formatting;

public class SecondsDisplayTests
{
    [Theory]
    [InlineData(27000, "hours", 7.5)]
    [InlineData(5400, "minutes", 90.0)]
    [InlineData(45, "seconds", 45.0)]
    public void ConvertToUnit_converts_to_the_metric_time_unit(int secs, string unit, double expected)
        => SecondsDisplay.ConvertToUnit(secs, unit).Should().Be((decimal)expected);

    [Theory]
    [InlineData(null)]
    [InlineData("l")]
    [InlineData("km")]
    public void ConvertToUnit_returns_null_for_non_time_units(string? unit)
        => SecondsDisplay.ConvertToUnit(90, unit).Should().BeNull();

    [Theory]
    [InlineData("hours")]
    [InlineData("MINUTES")] // callers pass whatever case the unit table holds
    [InlineData("seconds")]
    public void IsTimeUnit_is_true_for_the_units_a_duration_can_convert_into(string unit)
        => SecondsDisplay.IsTimeUnit(unit).Should().BeTrue();

    [Theory]
    [InlineData(null)] // the common case: a duration metric with no unit chosen at all
    [InlineData("km")]
    public void IsTimeUnit_is_false_for_everything_else(string? unit)
        => SecondsDisplay.IsTimeUnit(unit).Should().BeFalse();
}
