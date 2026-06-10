using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Converters;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class HabitTintBrushConverterTests
{
    private readonly HabitTintBrushConverter _sut = new();

    [Fact]
    public void Produces_brush_of_the_color_at_the_requested_alpha()
    {
        var brush = (SolidColorBrush)_sut.Convert("#3B82F6", typeof(Brush), "26", CultureInfo.InvariantCulture);
        brush.Color.A.Should().Be(26);
        brush.Color.R.Should().Be(0x3B);
        brush.Color.G.Should().Be(0x82);
        brush.Color.B.Should().Be(0xF6);
    }

    [Fact]
    public void Malformed_or_null_input_is_transparent()
    {
        _sut.Convert("nope", typeof(Brush), "26", CultureInfo.InvariantCulture).Should().Be(Brushes.Transparent);
        _sut.Convert(null, typeof(Brush), "26", CultureInfo.InvariantCulture).Should().Be(Brushes.Transparent);
    }

    [Fact]
    public void Missing_alpha_parameter_defaults_to_about_ten_percent()
    {
        var brush = (SolidColorBrush)_sut.Convert("#3B82F6", typeof(Brush), null, CultureInfo.InvariantCulture);
        brush.Color.A.Should().Be(0x1A);
    }
}
