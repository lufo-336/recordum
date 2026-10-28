using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Recordum.UI.Theming;
using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>I neutri della direzione visiva (§4.2) si leggono: testo e testo attenuato a 4,5:1 sui fondi, l'inchiostro
/// col suo testo, gli stati sul pannello. Il tenue è solo per segnaposto e separatori.</summary>
public class TokenContrastTests
{
    public static TheoryData<string> Themes => new() { "Light", "Dark" };

    private static Color Brush(string key, string theme)
    {
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(global::Avalonia.Application.Current!.TryGetResource(key, variant, out var value), $"{key} ({theme})");
        return ((ISolidColorBrush)value!).Color;
    }

    private static void AtLeast(double min, string fore, string back, string theme)
    {
        var ratio = HabitColors.Contrast(Brush(fore, theme), Brush(back, theme));
        Assert.True(ratio >= min, $"{fore} su {back} ({theme}): {ratio:0.00} < {min}");
    }

    [Fact]
    public void Contrast_of_black_on_white_is_21_and_of_a_colour_on_itself_is_1()
    {
        Assert.Equal(21d, HabitColors.Contrast(Colors.Black, Colors.White), 2);
        Assert.Equal(1d, HabitColors.Contrast(Color.Parse("#2F7FC1"), Color.Parse("#2F7FC1")), 2);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Text_and_muted_text_read_on_every_surface(string theme)
    {
        foreach (var back in new[] { "Brush.Background", "Brush.Pane", "Brush.Surface" })
        {
            AtLeast(4.5, "Brush.Text", back, theme);
            AtLeast(4.5, "Brush.TextMuted", back, theme);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Ink_and_states_read(string theme)
    {
        AtLeast(4.5, "Brush.OnInk", "Brush.Ink", theme);
        AtLeast(4.5, "Brush.Danger", "Brush.Pane", theme);
        AtLeast(4.5, "Brush.Warning", "Brush.Pane", theme);
        AtLeast(4.5, "Brush.Warning", "Brush.WarningSurface", theme);
        AtLeast(2.5, "Brush.TextFaint", "Brush.Pane", theme);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Fluent_accent_is_the_ink(string theme)
    {
        // Caselle, focus e selezioni di Fluent prendono SystemAccentColor: deve essere l'inchiostro, non il blu.
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(global::Avalonia.Application.Current!.TryGetResource("SystemAccentColor", variant, out var accent));
        Assert.Equal(Brush("Brush.Ink", theme), (Color)accent!);
    }

    private static Color Res(string key, string theme)
    {
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(global::Avalonia.Application.Current!.TryGetResource(key, variant, out var value), $"{key} ({theme})");
        return value is Color c ? c : ((ISolidColorBrush)value!).Color;
    }

    private static void ResAtLeast(double min, string fore, string back, string theme)
    {
        var ratio = HabitColors.Contrast(Res(fore, theme), Res(back, theme));
        Assert.True(ratio >= min, $"{fore} su {back} ({theme}): {ratio:0.00} < {min}");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Selected_text_reads_on_its_selection(string theme)
    {
        // Review I-1: la selezione era l'inchiostro e il testo selezionato restava del colore normale (1,2:1).
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var box = new Avalonia.Controls.TextBox { Text = "MMMM" };
        var window = new Avalonia.Controls.Window { Content = box, RequestedThemeVariant = variant };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var selection = ((ISolidColorBrush)box.SelectionBrush!).Color;
        var text = ((ISolidColorBrush)(box.SelectionForegroundBrush ?? box.Foreground)!).Color;
        window.Close();
        var ratio = HabitColors.Contrast(text, selection);
        Assert.True(ratio >= 4.5, $"selezione ({theme}): {ratio:0.00}");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_checked_box_shows_its_tick(string theme)
        // Review I-2: in scuro la spunta (quasi bianca) stava sul riempimento chiaro dell'inchiostro, 1,21:1.
        => ResAtLeast(4.5, "CheckBoxCheckGlyphForegroundChecked", "CheckBoxCheckBackgroundFillChecked", theme);

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Selected_list_items_read(string theme)
    {
        // Review I-3: in scuro ListBox e ComboBox selezionati stavano a 2,81:1.
        ResAtLeast(4.5, "ComboBoxItemForegroundSelected", "ComboBoxItemBackgroundSelected", theme);

        // Il ListBox non ha chiavi sue: si misura quello che il template dipinge davvero sulla voce selezionata.
        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var list = new Avalonia.Controls.ListBox { ItemsSource = new[] { "Manuale", "Nome" }, SelectedIndex = 0 };
        var window = new Avalonia.Controls.Window { Content = list, Width = 200, Height = 120, RequestedThemeVariant = variant };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var presenter = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(list.ContainerFromIndex(0)!)
            .OfType<Avalonia.Controls.Presenters.ContentPresenter>().First();
        var back = ((ISolidColorBrush)presenter.Background!).Color;
        var fore = ((ISolidColorBrush)presenter.Foreground!).Color;
        window.Close();
        var ratio = HabitColors.Contrast(fore, back);
        Assert.True(ratio >= 4.5, $"ListBox selezionato ({theme}): {ratio:0.00}");
    }
}
