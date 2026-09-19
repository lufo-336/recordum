using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace Recordum.UI.Tests;

public class EmbeddedFontTests
{
    private static FontFamily Token(string key)
    {
        Assert.True(global::Avalonia.Application.Current!.TryFindResource(key, out var value), key);
        return (FontFamily)value!;
    }

    [AvaloniaTheory]
    [InlineData("Font.Default")]
    [InlineData("Font.Serif")]
    public void The_shipped_serif_token_names_the_assembly_not_a_system_stack(string key)
    {
        var family = Token(key);

        // Il nome della risorsa deve nominare l'assembly: se un domani qualcuno lo sostituisse con
        // uno stack di sistema, l'app tornerebbe ad avere un aspetto diverso su ogni macchina e
        // nessuno se ne accorgerebbe finché non aprisse l'app su un OS diverso.
        // Per una famiglia con più nomi Avalonia espone la chiave come "compositefont:<stack intero>".
        Assert.Contains("avares://Recordum.UI/Fonts/#Source Serif 4", family.Key?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Source Serif 4", family.FamilyNames[0]);
    }

    [AvaloniaTheory]
    [InlineData(FontStyle.Normal, FontWeight.Normal)]
    [InlineData(FontStyle.Italic, FontWeight.Normal)]
    [InlineData(FontStyle.Normal, FontWeight.Bold)]
    [InlineData(FontStyle.Italic, FontWeight.Bold)]
    public void The_embedded_faces_resolve_from_the_assembly_without_synthesis(FontStyle style, FontWeight weight)
    {
        // Il token che punta alla risorsa non basta: il file deve davvero uscire dall'assembly e
        // contenere la faccia richiesta. Se la risorsa non si risolvesse, Skia ripiegherebbe su un
        // font di sistema e FamilyName non sarebbe "Source Serif 4"; se mancasse il file Italic o
        // Bold, Skia sintetizzerebbe la faccia (vedi FontSimulations sotto).
        var typeface = new Typeface(Token("Font.Default"), style, weight);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph));
        Assert.Equal("Source Serif 4", glyph.FamilyName);
        Assert.Equal(style, glyph.Style);
        Assert.Equal(weight, glyph.Weight);
        // Style/Weight riportano quanto richiesto anche quando la faccia manca; l'unico segnale
        // onesto che il file esiste davvero è che Skia non abbia dovuto sintetizzare (Oblique/Bold).
        Assert.Equal(FontSimulations.None, glyph.FontSimulations);
    }
}
