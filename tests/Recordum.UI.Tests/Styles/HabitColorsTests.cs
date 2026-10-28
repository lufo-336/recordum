using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Recordum.UI.Converters;
using Recordum.UI.Theming;
using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>Il colore dell'abitudine (§4.3): la tinta della riga per tema e la variante "testo" dei valori, leggibile a
/// 4,5:1 sulla sua tinta per qualunque colore.</summary>
public class HabitColorsTests
{
    // La palette della spec §7.1.
    public static TheoryData<string> Palette => new()
    {
        "#C2405B", "#D0603A", "#C98A1E", "#9A9A2A", "#5F9A3C", "#2F9C8C", "#2F8FB0", "#2F7FC1",
        "#4F63C4", "#7A5BC4", "#A352A8", "#C2508A", "#8A6A4F", "#6B7280", "#4B5563", "#9A8F80",
    };

    public static TheoryData<string> Extremes => new() { "#FFFFFF", "#000000", "#FFFF00", "#00FFFF", "#808080" };

    private static readonly Color LightPane = Color.Parse("#FFFFFF");
    private static readonly Color DarkPane = Color.Parse("#222221");

    private static void ReadsOnItsTint(string hex)
    {
        var habit = Color.Parse(hex);
        foreach (var (pane, tint, dark) in new[] { (LightPane, 0.09, false), (DarkPane, 0.16, true) })
        {
            var text = HabitColors.TextVariant(habit, pane, tint, dark);
            var ratio = HabitColors.Contrast(text, HabitColors.Blend(habit, tint, pane));
            Assert.True(ratio >= 4.5, $"{hex} ({(dark ? "scuro" : "chiaro")}): {ratio:0.00}");
        }
    }

    [Theory]
    [MemberData(nameof(Palette))]
    public void Text_variant_reaches_4_5_on_its_tint_for_every_palette_colour(string hex) => ReadsOnItsTint(hex);

    [Theory]
    [MemberData(nameof(Extremes))]
    public void Text_variant_reaches_4_5_for_extreme_colours(string hex) => ReadsOnItsTint(hex);

    [Fact]
    public void The_raw_colour_is_not_enough()
    {
        // Il test morde: misurato dalla verifica del costruttore, l'ocra pieno sulla sua tinta in chiaro sta sotto 3:1.
        var ochre = Color.Parse("#C98A1E");
        Assert.True(HabitColors.Contrast(ochre, HabitColors.Blend(ochre, 0.09, LightPane)) < 3);
    }

    [Fact]
    public void Dark_variant_is_lighter_than_the_habit_colour_and_light_variant_darker()
    {
        var blue = Color.Parse("#2F7FC1");
        var white = Colors.White;
        Assert.True(HabitColors.Contrast(HabitColors.TextVariant(blue, DarkPane, 0.16, dark: true), white)
                    < HabitColors.Contrast(blue, white));
        Assert.True(HabitColors.Contrast(HabitColors.TextVariant(blue, LightPane, 0.09, dark: false), white)
                    >= HabitColors.Contrast(blue, white));
    }

    [Fact]
    public void Blend_at_zero_is_the_background_and_at_one_the_colour()
    {
        var red = Color.Parse("#C2405B");
        Assert.Equal(LightPane, HabitColors.Blend(red, 0, LightPane));
        Assert.Equal(red, HabitColors.Blend(red, 1, LightPane));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("blu")]
    public void TryParse_rejects_broken_colours(string? hex) => Assert.False(HabitColors.TryParse(hex, out _));

    [AvaloniaFact]
    public void A_broken_colour_gets_the_normal_text_brush()
    {
        var brush = (ISolidColorBrush)new HabitTextBrushConverter().Convert(
            ["blu", ThemeVariant.Light], typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.True(global::Avalonia.Application.Current!.TryGetResource("Brush.Text", ThemeVariant.Light, out var text));
        Assert.Equal(((ISolidColorBrush)text!).Color, brush.Color);
    }

    [AvaloniaFact]
    public void Tint_opacity_follows_a_theme_change_at_runtime()
    {
        // Review Focus 1: la tinta è un livello con Opacity da un token tematico, quindi un cambio di tema a app aperta
        // la aggiorna senza ricostruire niente.
        var layer = new Border { Background = Brushes.Red };
        layer[!Border.OpacityProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Opacity.RowTint");
        var window = new Window { Content = layer, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0.09, layer.Opacity, 3);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0.16, layer.Opacity, 3);
        }
        finally { window.Close(); }
    }
}
