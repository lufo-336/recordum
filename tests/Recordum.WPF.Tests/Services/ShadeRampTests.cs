using FluentAssertions;
using Recordum.Presentation.Services;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class ShadeRampTests
{
    [Fact]
    public void For_returns_seven_valid_hex_colors_light_to_dark()
    {
        var ramp = ShadeRamp.For("#3b82f6");

        ramp.Should().HaveCount(7);
        // HsvColor.ToHex emits canonical uppercase (#RRGGBB, enforced by HsvColorTests); accept either case.
        ramp.Should().OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$"));

        // Value (brightness) strictly decreases lightest -> darkest.
        var vs = ramp.Select(c => HsvColor.FromHex(c)!.Value.V).ToList();
        for (int i = 1; i < vs.Count; i++) vs[i].Should().BeLessThan(vs[i - 1]);
    }

    [Fact]
    public void For_centres_on_the_base_hue()
    {
        var ramp = ShadeRamp.For("#3b82f6");
        var baseH = HsvColor.FromHex("#3b82f6")!.Value.H;
        var midH = HsvColor.FromHex(ramp[3])!.Value.H;
        midH.Should().BeApproximately(baseH, 1.0);
    }

    [Fact]
    public void For_returns_empty_on_invalid_hex()
    {
        ShadeRamp.For("nope").Should().BeEmpty();
        ShadeRamp.For(null!).Should().BeEmpty();
    }
}
