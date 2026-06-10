using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Converters;
using Xunit;

namespace Recordum.WPF.Tests.Converters;

public class HexToColorConverterTests
{
    private readonly HexToColorConverter _sut = new();

    [Fact]
    public void Parses_valid_hex_to_color()
    {
        var c = (Color)_sut.Convert("#3B82F6", typeof(Color), null, CultureInfo.InvariantCulture);
        c.R.Should().Be(0x3B);
        c.G.Should().Be(0x82);
        c.B.Should().Be(0xF6);
    }

    [Fact]
    public void Malformed_or_null_falls_back_to_default_accent()
    {
        var fallback = (Color)ColorConverter.ConvertFromString("#3B82F6");
        ((Color)_sut.Convert("nope", typeof(Color), null, CultureInfo.InvariantCulture)).Should().Be(fallback);
        ((Color)_sut.Convert(null, typeof(Color), null, CultureInfo.InvariantCulture)).Should().Be(fallback);
    }
}
