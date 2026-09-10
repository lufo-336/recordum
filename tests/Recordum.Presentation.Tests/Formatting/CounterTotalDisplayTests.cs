using FluentAssertions;
using Recordum.Presentation.Formatting;
using Xunit;

namespace Recordum.Presentation.Tests.Formatting;

public class CounterTotalDisplayTests
{
    [Fact]
    public void A_duration_total_with_no_time_unit_reads_as_a_duration_not_as_bare_seconds()
    {
        // 12 minutes stored as 720 seconds on a metric with no unit: the records list already shows
        // "12:00", so the total must agree instead of printing the raw second count (W23-VF2).
        CounterTotalDisplay.AsRawDuration(isDurationMetric: true, unitCode: null, total: 720m)
            .Should().Be("12:00");
    }

    [Fact]
    public void A_duration_total_with_a_non_time_unit_still_reads_as_a_duration()
    {
        // "km" cannot convert seconds, so the stored total is still raw seconds.
        CounterTotalDisplay.AsRawDuration(isDurationMetric: true, unitCode: "km", total: 43200m)
            .Should().Be("12:00:00");
    }

    [Fact]
    public void A_duration_total_already_converted_by_its_time_unit_is_left_to_the_caller()
    {
        // The unit converted 90s → 1.5: the caller renders "1,5 minutes" itself.
        CounterTotalDisplay.AsRawDuration(isDurationMetric: true, unitCode: "minutes", total: 1.5m)
            .Should().BeNull();
    }

    [Fact]
    public void A_non_duration_total_is_left_to_the_caller()
        => CounterTotalDisplay.AsRawDuration(isDurationMetric: false, unitCode: null, total: 720m)
            .Should().BeNull();

    [Theory]
    [InlineData("2147483648")]  // int.MaxValue + 1
    [InlineData("-1")]
    public void An_out_of_range_total_is_left_to_the_caller_rather_than_overflowing(string total)
        => CounterTotalDisplay.AsRawDuration(true, null, decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture))
            .Should().BeNull();
}
