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

    [Theory]
    [InlineData(120, "minutes", 2)]
    [InlineData(27000, "hours", 7.5)]
    [InlineData(45, "seconds", 45)]
    public void ConvertTotal_expresses_a_duration_total_in_the_metric_unit(double raw, string unit, double expected)
        => SecondsDisplay.ConvertTotal((decimal)raw, isDurationMetric: true, unit).Should().Be((decimal)expected);

    [Theory]
    [InlineData(null)] // a "+ Tempo" field ships with no unit: the total stays a second count
    [InlineData("l")]
    public void ConvertTotal_leaves_a_duration_total_raw_when_the_unit_is_not_a_time_unit(string? unit)
        => SecondsDisplay.ConvertTotal(120m, isDurationMetric: true, unit).Should().Be(120m);

    [Fact]
    public void ConvertTotal_leaves_a_non_duration_total_alone_even_on_a_time_unit()
        => SecondsDisplay.ConvertTotal(120m, isDurationMetric: false, "minutes").Should().Be(120m);

    [Theory]
    [InlineData(-1)]
    [InlineData(2147483648)] // int.MaxValue + 1: cannot be a real second count, and the cast would throw
    public void ConvertTotal_leaves_totals_outside_the_second_count_range_raw(double raw)
        => SecondsDisplay.ConvertTotal((decimal)raw, isDurationMetric: true, "minutes").Should().Be((decimal)raw);

    [Fact]
    public void TimeUnitCodes_lists_the_three_units_a_duration_can_be_expressed_in()
        => SecondsDisplay.TimeUnitCodes.Should().Equal("hours", "minutes", "seconds");

    [Fact]
    public void Every_listed_code_is_a_time_unit_and_converts()
    {
        // Anti-divergence guard: the list the UI filters its pickers with and the predicate the
        // formatters branch on must stay the same set. Before this, the codes lived duplicated as a
        // private array in HabitBuilderViewModel while IsTimeUnit derived the notion another way.
        foreach (var code in SecondsDisplay.TimeUnitCodes)
        {
            SecondsDisplay.IsTimeUnit(code).Should().BeTrue($"'{code}' is listed as a time unit");
            SecondsDisplay.ConvertToUnit(1, code).Should().NotBeNull($"'{code}' must be convertible");
        }
    }
}
