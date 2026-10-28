using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Controls;
using Recordum.UI.Converters;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>Le cinque forme per-tipo di <c>Controls/HeatmapStrip.axaml</c> nelle due varianti.
/// Prima passavano da una view di sonda; ora la composizione sta in <see cref="ControlGalleries"/>
/// e il controllo e' esercitato direttamente. La prova NON e' il peso del PNG: e' la struttura —
/// cinque strisce realizzate, ciascuna larga i 96px che la riga Home le riserva.</summary>
public class HeatmapStripScreenshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_five_per_type_strips_realize_in_both_variants(string variantName)
    {
        var window = new Window
        {
            Width = 480,
            Height = 420,
            RequestedThemeVariant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = ControlGalleries.HeatmapStripGallery(),
        };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var strips = window.GetVisualDescendants().OfType<HeatmapStrip>().ToArray();
        strips.Should().HaveCount(5, "una per tipo di abitudine");
        strips.Should().OnlyContain(s => s.Bounds.Width > 0 && s.Bounds.Height > 0);

        ScreenshotProbeTests.Capture(window, $"heatmap-{variantName.ToLowerInvariant()}");
    }
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Values_are_written_in_the_text_variant_and_empty_days_in_the_faint_text(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var strip = new HeatmapStrip
        {
            AccentColor = "#C98A1E",
            Values = [new StripCell("5", true, false), new StripCell("·", false, false), new StripCell("8", true, true)],
        };
        var window = new Window { Width = 200, Height = 40, RequestedThemeVariant = variant, Content = strip };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = strip.GetVisualDescendants().OfType<TextBlock>().ToArray();
        texts.Select(t => t.Text).Should().Equal("5", "·", "8");
        var expected = ((ISolidColorBrush)HabitTextBrushConverter.BrushFor("#C98A1E", variant)).Color;
        ((ISolidColorBrush)texts[0].Foreground!).Color.Should().Be(expected);
        Application.Current!.TryGetResource("Brush.TextFaint", variant, out var faint).Should().BeTrue();
        ((ISolidColorBrush)texts[1].Foreground!).Color.Should().Be(((ISolidColorBrush)faint!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void Values_are_recoloured_when_the_theme_changes()
    {
        var strip = new HeatmapStrip { AccentColor = "#2F7FC1", Values = [new StripCell("5", true, true)] };
        var window = new Window { Width = 200, Height = 40, RequestedThemeVariant = ThemeVariant.Light, Content = strip };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Color Fore() => ((ISolidColorBrush)strip.GetVisualDescendants().OfType<TextBlock>().Single().Foreground!).Color;
        var light = Fore();
        window.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
        Fore().Should().NotBe(light).And.Be(((ISolidColorBrush)HabitTextBrushConverter.BrushFor("#2F7FC1", ThemeVariant.Dark)).Color);
        window.Close();
    }
}
