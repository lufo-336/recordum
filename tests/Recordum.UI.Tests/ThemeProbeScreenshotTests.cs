using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using FluentAssertions;
using Recordum.UI.Views;

namespace Recordum.UI.Tests;

/// <summary>
/// Sonda di styling (branch avalonia-linux-slice, NON una feature): renderizza ThemeProbeView in
/// entrambe le varianti e salva i due PNG che il deliverable #3 del probe chiede — l'unico
/// consumer di ThemeProbeView in tutto il repo, per design (vedi il commento in App.axaml e in
/// ThemeProbeView.axaml.cs: nessun'altra vista la referenzia).
/// </summary>
public class ThemeProbeScreenshotTests
{
    [AvaloniaFact]
    public void Theme_probe_renders_in_light_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Light, "theme-light");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG 1000x700 con contenuto reale non può essere pochi byte");
    }

    [AvaloniaFact]
    public void Theme_probe_renders_in_dark_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Dark, "theme-dark");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG 1000x700 con contenuto reale non può essere pochi byte");
    }

    private static string RenderAndCapture(ThemeVariant variant, string name)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 700,
            RequestedThemeVariant = variant,
            Content = new ThemeProbeView(),
        };
        window.Show();

        return ScreenshotProbeTests.Capture(window, name);
    }
}
