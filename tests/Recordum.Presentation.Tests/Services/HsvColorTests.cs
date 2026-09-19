using FluentAssertions;
using Recordum.Presentation.Services;
using Xunit;

namespace Recordum.Presentation.Tests.Services;

public class HsvColorTests
{
    [Theory]
    [InlineData("#FF0000", 0, 1, 1)]     // red
    [InlineData("#00FF00", 120, 1, 1)]   // green
    [InlineData("#0000FF", 240, 1, 1)]   // blue
    [InlineData("#FFFFFF", 0, 0, 1)]     // white
    [InlineData("#000000", 0, 0, 0)]     // black
    public void FromHex_yields_expected_hsv(string hex, double h, double s, double v)
    {
        var hsv = HsvColor.FromHex(hex);
        hsv.Should().NotBeNull();
        hsv!.Value.H.Should().BeApproximately(h, 0.5);
        hsv.Value.S.Should().BeApproximately(s, 0.01);
        hsv.Value.V.Should().BeApproximately(v, 0.01);
    }

    [Theory]
    [InlineData(0, 1, 1, "#FF0000")]
    [InlineData(120, 1, 1, "#00FF00")]
    [InlineData(240, 1, 1, "#0000FF")]
    [InlineData(0, 0, 1, "#FFFFFF")]
    [InlineData(0, 0, 0, "#000000")]
    public void ToHex_yields_expected_color(double h, double s, double v, string expected)
        => HsvColor.ToHex(h, s, v).Should().Be(expected);

    [Theory]
    [InlineData("#3B82F6")]
    [InlineData("#E0B341")]
    [InlineData("#8B5CF6")]
    [InlineData("#10B981")]
    public void Round_trips_hex_through_hsv(string hex)
    {
        var hsv = HsvColor.FromHex(hex)!.Value;
        HsvColor.ToHex(hsv.H, hsv.S, hsv.V).Should().Be(hex);
    }

    [Fact]
    public void FromHex_rejects_malformed_input()
    {
        HsvColor.FromHex(null).Should().BeNull();
        HsvColor.FromHex("#fff").Should().BeNull();
        HsvColor.FromHex("not-a-color").Should().BeNull();
    }

    [Fact]
    public void Hue_360_wraps_to_red()
        => HsvColor.ToHex(360, 1, 1).Should().Be("#FF0000");
}
