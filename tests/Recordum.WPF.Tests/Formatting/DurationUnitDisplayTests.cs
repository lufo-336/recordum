using System.Globalization;
using FluentAssertions;
using Recordum.WPF.Formatting;
using Recordum.WPF.Resources;
using Xunit;

namespace Recordum.WPF.Tests.Formatting;

public class DurationUnitDisplayTests
{
    // Expected strings are built from the same .resx labels + CurrentCulture number the production
    // code uses, so the test stays green under any UI culture (and proves the label is localized,
    // not the raw "hours"/"minutes"/"seconds" code).
    [Theory]
    [InlineData(27000, "hours", 7.5)]
    [InlineData(5400, "minutes", 90.0)]
    [InlineData(45, "seconds", 45.0)]
    public void InUnit_renders_the_localized_time_unit(int secs, string code, double value)
    {
        var label = code switch
        {
            "hours" => Strings.TimeUnit_Hours,
            "minutes" => Strings.TimeUnit_Minutes,
            _ => Strings.TimeUnit_Seconds,
        };
        var expected = ((decimal)value).ToString("0.##", CultureInfo.CurrentCulture) + " " + label;

        DurationUnitDisplay.InUnit(secs, code).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("l")]
    [InlineData("km")]
    public void InUnit_returns_null_for_non_time_units(string? code)
        => DurationUnitDisplay.InUnit(90, code).Should().BeNull();
}
