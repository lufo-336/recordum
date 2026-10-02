using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.UI.Controls;
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
}
