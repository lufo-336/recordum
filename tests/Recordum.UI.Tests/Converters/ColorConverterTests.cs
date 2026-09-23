using System.Globalization;
using Avalonia.Media;
using FluentAssertions;
using Recordum.UI.Converters;
using Xunit;

namespace Recordum.UI.Tests.Converters;

public class ColorConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static Color ColorOf(object? result)
    {
        result.Should().BeAssignableTo<ISolidColorBrush>();
        return ((ISolidColorBrush)result!).Color;
    }

    // ── HexToBrushConverter ────────────────────────────────────────────────────────────────────

    [Fact]
    public void HexToBrush_turns_a_hex_string_into_that_color()
    {
        var result = HexToBrushConverter.Instance.Convert("#3B82F6", typeof(IBrush), null, Culture);

        ColorOf(result).Should().Be(Color.FromRgb(0x3B, 0x82, 0xF6));
    }

    [Fact]
    public void HexToBrush_keeps_the_alpha_of_an_eight_digit_hex()
    {
        var result = HexToBrushConverter.Instance.Convert("#803B82F6", typeof(IBrush), null, Culture);

        ColorOf(result).Should().Be(Color.FromArgb(0x80, 0x3B, 0x82, 0xF6));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    [InlineData("#12")]
    [InlineData(42)]
    public void HexToBrush_falls_back_to_gray_for_anything_it_cannot_read(object? input)
    {
        var result = HexToBrushConverter.Instance.Convert(input, typeof(IBrush), null, Culture);

        ColorOf(result).Should().Be(Colors.Gray);
    }

    [Fact]
    public void HexToBrush_cannot_convert_back()
    {
        var act = () => HexToBrushConverter.Instance.ConvertBack(Brushes.Red, typeof(string), null, Culture);

        act.Should().Throw<NotSupportedException>();
    }

    // ── HabitTintConverter ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void HabitTint_applies_the_parameter_as_alpha_and_keeps_the_rgb()
    {
        var result = HabitTintConverter.Instance.Convert("#3B82F6", typeof(IBrush), "80", Culture);

        ColorOf(result).Should().Be(Color.FromArgb(80, 0x3B, 0x82, 0xF6));
    }

    [Fact]
    public void HabitTint_accepts_the_alpha_as_a_number_too()
    {
        // ConverterParameter={x:Static ...} porta un intero, ConverterParameter=80 una stringa.
        var fromInt = HabitTintConverter.Instance.Convert("#3B82F6", typeof(IBrush), 80, Culture);
        var fromByte = HabitTintConverter.Instance.Convert("#3B82F6", typeof(IBrush), (byte)80, Culture);

        ColorOf(fromInt).A.Should().Be(80);
        ColorOf(fromByte).A.Should().Be(80);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("300")]   // fuori dal byte
    [InlineData("-1")]
    [InlineData(300)]
    public void HabitTint_defaults_to_0x1A_when_the_parameter_is_missing_or_invalid(object? parameter)
    {
        var result = HabitTintConverter.Instance.Convert("#3B82F6", typeof(IBrush), parameter, Culture);

        // 0x1A = 26 (~10%): il default che il blocco tinto ha sempre avuto. Il test non può
        // passare per caso: nessun altro valore dell'alpha nel repo è 26 tranne Recordum.HabitBlock.
        ColorOf(result).A.Should().Be(0x1A);
        ColorOf(result).R.Should().Be(0x3B);
    }

    [Fact]
    public void HabitTint_discards_the_alpha_written_in_the_hex()
    {
        // Comanda il parametro: un colore salvato come #FF3B82F6 con alpha 26 è comunque al 10%.
        var result = HabitTintConverter.Instance.Convert("#FF3B82F6", typeof(IBrush), "26", Culture);

        ColorOf(result).A.Should().Be(26);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    [InlineData(7)]
    public void HabitTint_is_transparent_for_a_missing_or_malformed_color(object? input)
    {
        var result = HabitTintConverter.Instance.Convert(input, typeof(IBrush), "80", Culture);

        result.Should().BeSameAs(Brushes.Transparent);
    }
}
