using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using FluentAssertions;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia.Tests;

/// <summary>
/// Sonda editor (branch avalonia-linux-slice, NON una feature): renderizza
/// <see cref="MarkdownEditorProbeView"/> — sorgente AvaloniaEdit + anteprima Markdown.Avalonia,
/// vedi <c>Controls/MarkdownEditorControl.axaml</c> — in entrambe le varianti tema e salva i due
/// PNG che il deliverable #2 del probe editor chiede. Stesso pattern di
/// <see cref="HeatmapStripScreenshotTests"/>.
/// </summary>
public class MarkdownEditorScreenshotTests
{
    [AvaloniaFact]
    public void Editor_probe_renders_in_light_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Light, "editor-light");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG con markdown reale (titoli, lista, code block) non può essere pochi byte");
    }

    [AvaloniaFact]
    public void Editor_probe_renders_in_dark_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Dark, "editor-dark");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG con markdown reale (titoli, lista, code block) non può essere pochi byte");
    }

    private static string RenderAndCapture(ThemeVariant variant, string name)
    {
        var window = new Window
        {
            Width = 900,
            Height = 560,
            RequestedThemeVariant = variant,
            Content = new MarkdownEditorProbeView(),
        };
        window.Show();

        return ScreenshotProbeTests.Capture(window, name);
    }
}
