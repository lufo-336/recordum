using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class DaySummaryFormatterTests
{
    [Fact]
    public void Counter_appends_the_primary_unit()
        => DaySummaryFormatter.Label(HabitType.Counter, 4m, "km", null).Should().Be("4 km");

    [Fact]
    public void Counter_without_unit_is_the_bare_number()
        => DaySummaryFormatter.Label(HabitType.Counter, 4m, null, null).Should().Be("4");

    [Fact]
    public void A_duration_text_replaces_the_bare_number_for_a_unitless_duration_counter()
        // 720 raw seconds with nothing to convert into: the strip must read "12:00", not "720" (W23-VF2).
        => DaySummaryFormatter.Label(HabitType.Counter, 720m, null, null, "12:00").Should().Be("12:00");

    [Fact]
    public void A_duration_text_does_not_resurrect_an_empty_day()
        => DaySummaryFormatter.Label(HabitType.Counter, 0m, null, null, "0:00").Should().BeNull();

    [Fact]
    public void State_maps_the_one_based_value_to_the_scale_label()
        => DaySummaryFormatter.Label(HabitType.State, 3m, null, new[] { "Bad", "Ok", "Good" }).Should().Be("Good");

    [Fact]
    public void State_out_of_range_falls_back_to_the_number()
        => DaySummaryFormatter.Label(HabitType.State, 9m, null, new[] { "Bad", "Ok", "Good" }).Should().Be("9");

    [Fact]
    public void State_without_scale_is_the_bare_number()
        => DaySummaryFormatter.Label(HabitType.State, 3m, null, null).Should().Be("3");

    [Fact]
    public void Resistance_shows_series_count_only_when_more_than_one()
    {
        DaySummaryFormatter.Label(HabitType.Resistance, 3m, null, null).Should().Be("3");
        DaySummaryFormatter.Label(HabitType.Resistance, 1m, null, null).Should().BeNull();
    }

    [Fact]
    public void Action_shows_multiplier_only_when_more_than_one()
    {
        DaySummaryFormatter.Label(HabitType.Action, 2m, null, null).Should().Be("2×");
        DaySummaryFormatter.Label(HabitType.Action, 1m, null, null).Should().BeNull();
    }

    [Fact]
    public void Non_positive_magnitude_is_null()
        => DaySummaryFormatter.Label(HabitType.Counter, 0m, "km", null).Should().BeNull();
}
