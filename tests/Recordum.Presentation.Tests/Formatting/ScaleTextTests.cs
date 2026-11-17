using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Xunit;

namespace Recordum.Presentation.Tests.Formatting;

[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class ScaleTextTests : IDisposable
{
    private readonly CultureInfo? _prev = Strings.Culture;
    private readonly CultureInfo _prevCurrent = CultureInfo.CurrentCulture;
    private readonly CultureInfo _prevUi = CultureInfo.CurrentUICulture;

    public void Dispose()
    {
        Strings.Culture = _prev;
        CultureInfo.CurrentCulture = _prevCurrent;
        CultureInfo.CurrentUICulture = _prevUi;
    }

    private static void Pin(string name)
    {
        var c = CultureInfo.GetCultureInfo(name);
        Strings.Culture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.CurrentUICulture = c;
    }

    private static ScaleTemplate Mood() => new()
    {
        Code = "mood", Name = "Umore", MinValue = 0, MaxValue = 100,
        Anchors = [new ScaleAnchor { Value = 25, Label = "Male" }, new ScaleAnchor { Value = 50, Label = "Così così" }, new ScaleAnchor { Value = 75, Label = "Bene" }],
    };

    [Fact]
    public void Umore_37_it()
    {
        Pin("it-IT");
        ScaleText.Short(Mood(), 37).Should().Be("Male · 37");
        ScaleText.Long(Mood(), 37).Should().Be("37, fra Male e Così così");
        ScaleText.Short(Mood(), 50).Should().Be("Così così");
        ScaleText.Long(Mood(), 50).Should().Be("Così così");
    }

    [Fact]
    public void Umore_37_en()
    {
        Pin("en-US");
        ScaleText.Long(Mood(), 37).Should().Be("37, between Male and Così così");
    }

    [Fact]
    public void Senza_scaglioni_e_il_numero()
    {
        Pin("it-IT");
        var s = new ScaleTemplate { MinValue = 1, MaxValue = 25 };
        ScaleText.Short(s, 12).Should().Be("12");
        ScaleText.Long(s, 12).Should().Be("12");
    }

    [Theory]
    [InlineData("it-IT", "1,37")]
    [InlineData("en-US", "1.37")]
    public void Decimali(string culture, string expected)
    {
        Pin(culture);
        ScaleText.Short(new ScaleTemplate { MinValue = 0, MaxValue = 5, Precision = ScalePrecision.Decimal }, 1.37m).Should().Be(expected);
    }

    [Fact]
    public void Nrs_ufficiale_in_italiano()
    {
        Pin("it-IT");
        var nrs = new ScaleTemplate
        {
            Code = "nrs-pain", IsOfficial = true, MinValue = 0, MaxValue = 10,
            Anchors = [new ScaleAnchor { Value = 0, Label = "No pain" }, new ScaleAnchor { Value = 10, Label = "Worst pain" }],
        };
        ScaleText.Short(nrs, 0).Should().Be("Nessun dolore");
    }

    [Fact]
    public void Fraction_e_limitata()
    {
        var s = new ScaleTemplate { MinValue = -3, MaxValue = 3 };
        ScaleText.Fraction(s, -3).Should().Be(0);
        ScaleText.Fraction(s, 0).Should().Be(0.5);
        ScaleText.Fraction(s, 9).Should().Be(1);
    }
}
