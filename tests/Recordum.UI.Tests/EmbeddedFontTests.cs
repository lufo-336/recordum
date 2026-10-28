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

    [AvaloniaFact]
    public void Font_Default_resolves_to_Inter()
    {
        // L'interfaccia è in Inter (spec direzione visiva §4.1); il serif resta al testo dell'utente.
        var family = Token("Font.Default");
        Assert.Contains("avares://Avalonia.Fonts.Inter/Assets#Inter", family.Key?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Inter", family.FamilyNames[0]);
    }

    [AvaloniaFact]
    public void Font_Serif_still_resolves_to_Source_Serif_4()
    {
        // Il nome della risorsa deve nominare l'assembly: uno stack di sistema darebbe un aspetto diverso su ogni
        // macchina. Per una famiglia con più nomi Avalonia espone la chiave come "compositefont:<stack intero>".
        var family = Token("Font.Serif");
        Assert.Contains("avares://Recordum.UI/Fonts/#Source Serif 4", family.Key?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Source Serif 4", family.FamilyNames[0]);
    }

    [AvaloniaTheory]
    [InlineData(FontWeight.Normal)]
    [InlineData(FontWeight.Medium)]
    [InlineData(FontWeight.SemiBold)]
    public void The_interface_weights_resolve_without_synthesis(FontWeight weight)
    {
        // 400, 500 e 600 sono i pesi della scala: se un file mancasse Skia sintetizzerebbe (FontSimulations).
        var typeface = new Typeface(Token("Font.Default"), FontStyle.Normal, weight);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph));
        Assert.StartsWith("Inter", glyph.FamilyName, StringComparison.Ordinal);   // i file dichiarano "Inter Medium", "Inter SemiBold"
        Assert.Equal(weight, glyph.Weight);
        Assert.Equal(FontSimulations.None, glyph.FontSimulations);
    }

    [AvaloniaFact]
    public void A_view_hosted_in_a_plain_test_window_uses_the_app_font()
    {
        // UX-P2: le schermate del dettaglio nei test uscivano in sans di sistema perché una Window qualunque non
        // ereditava Font.Default (lo impostano solo MainWindow e RecordumWindow).
        var text = new TextBlock { Text = "x" };
        var window = new Window { Content = new UserControl { Content = text } };
        window.Show();
        try { Assert.Equal(Token("Font.Default"), text.FontFamily); }   // il token, non un default che gli somiglia
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(FontStyle.Normal, FontWeight.Normal)]
    [InlineData(FontStyle.Italic, FontWeight.Normal)]
    [InlineData(FontStyle.Normal, FontWeight.Bold)]
    [InlineData(FontStyle.Italic, FontWeight.Bold)]
    public void The_embedded_serif_faces_resolve_from_the_assembly_without_synthesis(FontStyle style, FontWeight weight)
    {
        // Il token che punta alla risorsa non basta: il file deve davvero uscire dall'assembly e
        // contenere la faccia richiesta. Se la risorsa non si risolvesse, Skia ripiegherebbe su un
        // font di sistema e FamilyName non sarebbe "Source Serif 4"; se mancasse il file Italic o
        // Bold, Skia sintetizzerebbe la faccia (vedi FontSimulations sotto).
        var typeface = new Typeface(Token("Font.Serif"), style, weight);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph));
        Assert.Equal("Source Serif 4", glyph.FamilyName);
        Assert.Equal(style, glyph.Style);
        Assert.Equal(weight, glyph.Weight);
        // Style/Weight riportano quanto richiesto anche quando la faccia manca; l'unico segnale
        // onesto che il file esiste davvero è che Skia non abbia dovuto sintetizzare (Oblique/Bold).
        Assert.Equal(FontSimulations.None, glyph.FontSimulations);
    }
}
