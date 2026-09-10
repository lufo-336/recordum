using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using FluentAssertions;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia.Tests;

/// <summary>
/// Sonda controlli (branch avalonia-linux-slice, NON una feature): renderizza
/// <see cref="HeatmapStripProbeView"/> — le cinque forme per-tipo di
/// <c>Controls/HeatmapStrip.axaml</c> — in entrambe le varianti e salva i due PNG che il
/// deliverable #2 del probe chiede. Stesso pattern di <see cref="ThemeProbeScreenshotTests"/>.
/// </summary>
public class HeatmapStripScreenshotTests
{
    [AvaloniaFact]
    public void Heatmap_strip_probe_renders_in_light_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Light, "heatmap-light");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG con cinque righe di contenuto reale non può essere pochi byte");
    }

    [AvaloniaFact]
    public void Heatmap_strip_probe_renders_in_dark_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Dark, "heatmap-dark");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG con cinque righe di contenuto reale non può essere pochi byte");
    }

    private static string RenderAndCapture(ThemeVariant variant, string name)
    {
        var window = new Window
        {
            Width = 480,
            Height = 420,
            RequestedThemeVariant = variant,
            Content = new HeatmapStripProbeView(),
        };
        window.Show();

        return ScreenshotProbeTests.Capture(window, name);
    }
}
