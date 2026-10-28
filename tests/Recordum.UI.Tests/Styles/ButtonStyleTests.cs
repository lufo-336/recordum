using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>Pulsanti e campi della direzione visiva (§4.4): a contorno di base, primario a inchiostro, ghost senza
/// bordo, campi con la cornice piena, la voce di menu pericolosa in rosso. Nei due temi.</summary>
public class ButtonStyleTests
{
    public static TheoryData<string> Themes => new() { "Light", "Dark" };

    private static Color Token(string key, ThemeVariant v)
    {
        Assert.True(Application.Current!.TryGetResource(key, v, out var value), key);
        return ((ISolidColorBrush)value!).Color;
    }

    private static (T Control, Window Window) Show<T>(T control, string theme) where T : Control
    {
        var window = new Window { Content = control, Width = 200, Height = 80, RequestedThemeVariant = Variant(theme) };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (control, window);
    }

    private static ThemeVariant Variant(string theme) => theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    // Il colore che il template disegna davvero: Fluent dipinge il ContentPresenter, non il Button.
    private static Color Painted(Button b) =>
        ((ISolidColorBrush)Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(b)
            .OfType<Avalonia.Controls.Presenters.ContentPresenter>().First().Background!).Color;

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_plain_button_is_outlined_not_filled(string theme)
    {
        var (b, w) = Show(new Button { Content = "Sfoglia…" }, theme);
        Assert.Equal(Token("Brush.Pane", Variant(theme)), Painted(b));
        Assert.Equal(new Thickness(1), b.BorderThickness);
        Assert.Equal(new CornerRadius(6), b.CornerRadius);
        w.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Primary_is_the_ink(string theme)
    {
        var (b, w) = Show(new Button { Content = "Crea", Classes = { "primary" } }, theme);
        Assert.Equal(Token("Brush.Ink", Variant(theme)), Painted(b));
        Assert.Equal(new CornerRadius(6), b.CornerRadius);
        w.Close();
    }

    [AvaloniaFact]
    public void Ghost_has_no_border()
    {
        var (b, w) = Show(new Button { Content = "Annulla", Classes = { "ghost" } }, "Light");
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)b.BorderBrush!).Color);
        Assert.Equal(new CornerRadius(6), b.CornerRadius);
        w.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void TextBox_has_a_full_frame(string theme)
    {
        var (t, w) = Show(new TextBox { Watermark = "Cerca" }, theme);
        Assert.Equal(new Thickness(1), t.BorderThickness);
        Assert.Equal(new CornerRadius(6), t.CornerRadius);
        Assert.Equal(Token("Brush.FieldBorder", Variant(theme)), ((ISolidColorBrush)t.BorderBrush!).Color);
        w.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_focused_field_keeps_its_surface(string theme)
    {
        // Visto nel PNG dello sblocco: in scuro il campo a fuoco diventava nero (la chiave Fluent di prima).
        Assert.True(Application.Current!.TryGetResource("TextControlBackgroundFocused", Variant(theme), out var focused));
        Assert.Equal(Token("Brush.SurfaceElevated", Variant(theme)), ((ISolidColorBrush)focused!).Color);
        Assert.True(Application.Current!.TryGetResource("TextControlBorderBrushFocused", Variant(theme), out var border));
        Assert.Equal(Token("Brush.Ink", Variant(theme)), ((ISolidColorBrush)border!).Color);
    }

    [AvaloniaFact]
    public void A_danger_menu_item_is_red()
    {
        var (m, w) = Show(new MenuItem { Header = "Elimina definitivamente", Classes = { "danger" } }, "Light");
        Assert.Equal(Token("Brush.Danger", ThemeVariant.Light), ((ISolidColorBrush)m.Foreground!).Color);
        w.Close();
    }
}
