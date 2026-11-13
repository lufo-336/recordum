using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

/// <summary>La striscia della Home scrive i valori del giorno (direzione visiva §5): ✓ per i tipi sì/no, il valore per
/// contatori e stati, "·" per il giorno vuoto; dal più vecchio al più nuovo, oggi ultimo.</summary>
public class HomeRowStripCellsTests
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private static HomeTreeRow Row(HabitType type, int[] counts, decimal?[]? values = null, bool duration = false) =>
        new(Guid.NewGuid(), null, "x", "#2F7FC1", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(counts[^7..]), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(counts),
            ValuesLast14Days: values is null ? default : ImmutableArray.Create(values),
            TodayCounterIsDuration: duration);

    private static HomeRowViewModel Vm(HomeTreeRow row, HomeStrip strip = HomeStrip.Days7)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = It;
        try
        {
            return new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService(),
                new HabitUiPrefs { Strip = strip });
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    private static int[] Days(params int[] last) => [.. Enumerable.Repeat(0, 14 - last.Length), .. last];

    [Fact]
    public void Action_on_days_are_check_marks_and_empty_days_a_middle_dot()
    {
        var cells = Vm(Row(HabitType.Action, Days(1, 0, 1))).StripCells;
        cells.Select(c => c.Glyph).TakeLast(3).Should().Equal("✓", "·", "✓");
        cells[^2].On.Should().BeFalse();
    }

    [Fact]
    public void Counter_days_show_their_value()
    {
        decimal?[] values = [.. Enumerable.Repeat<decimal?>(null, 12), 5m, 8m];
        Vm(Row(HabitType.Counter, Days(1, 1), values)).StripCells.Select(c => c.Glyph).TakeLast(2).Should().Equal("5", "8");
    }

    [Fact]
    public void A_duration_counter_is_written_in_minutes_or_hours()
    {
        decimal?[] values = [.. Enumerable.Repeat<decimal?>(null, 12), 2700m, 5400m];   // secondi grezzi
        Vm(Row(HabitType.Counter, Days(1, 1), values, duration: true)).StripCells.Select(c => c.Glyph).TakeLast(2)
            .Should().Equal("45m", "1,5h");
    }

    [Fact]
    public void Today_is_the_last_cell()
    {
        var cells = Vm(Row(HabitType.Action, Days(1))).StripCells;
        cells[^1].IsToday.Should().BeTrue();
        cells.Take(cells.Length - 1).Should().OnlyContain(c => !c.IsToday);
    }

    [Theory]
    [InlineData(HomeStrip.Days7, 14)]   // GIO-3: un "7 giorni" salvato prima vale come "sì", e la Home ne mostra quanti ci stanno
    [InlineData(HomeStrip.Days14, 14)]
    [InlineData(HomeStrip.None, 0)]
    public void Length_follows_the_strip(HomeStrip strip, int length) =>
        Vm(Row(HabitType.Action, Days(1)), strip).StripCells.Length.Should().Be(length);

    [Theory]
    [InlineData("0.5", "0,5")]
    [InlineData("7", "7")]
    [InlineData("12.25", "12")]
    [InlineData("1234.5", "1,2k")]
    [InlineData("12000", "12k")]
    [InlineData("999999", "999k")]
    public void Values_are_compact(string value, string expected)
    {
        var text = StripValueFormat.Compact(decimal.Parse(value, CultureInfo.InvariantCulture), It);
        text.Should().Be(expected);
        text.Length.Should().BeLessThanOrEqualTo(4);
    }

    [Theory]
    [InlineData("45", "45s")]
    [InlineData("2700", "45m")]
    [InlineData("5400", "1,5h")]
    [InlineData("0", "0s")]
    public void Durations_under_a_minute_are_seconds(string seconds, string expected)
        // Review M-3: un plank da 45 s segnava "0m" ogni giorno.
        => StripValueFormat.Duration(decimal.Parse(seconds, CultureInfo.InvariantCulture), It).Should().Be(expected);

    [Theory]
    [InlineData("-5", "-5")]
    [InlineData("-0.5", "-0,5")]
    [InlineData("-1234.5", "-1k")]
    [InlineData("-12000", "-12k")]
    public void Negative_values_stay_within_four_characters(string value, string expected)
    {
        var text = StripValueFormat.Compact(decimal.Parse(value, CultureInfo.InvariantCulture), It);
        text.Should().Be(expected);
        text.Length.Should().BeLessThanOrEqualTo(4);
    }
}
