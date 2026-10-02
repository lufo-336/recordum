using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using FluentAssertions;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>Sorgente AvaloniaEdit + anteprima Markdown.Avalonia, nelle due varianti.</summary>
public class MarkdownEditorScreenshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_editor_realizes_source_and_preview_in_both_variants(string variantName)
    {
        var window = new Window
        {
            Width = 900,
            Height = 520,
            RequestedThemeVariant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = ControlGalleries.MarkdownEditor(),
        };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var editors = window.GetVisualDescendants().OfType<TextEditor>().ToArray();
        editors.Should().NotBeEmpty("il controllo ospita il sorgente in un TextEditor");
        editors[0].Text.Should().Contain("Diario", "e il documento ci e' arrivato dentro");
        ScreenshotProbeTests.Capture(window, $"editor-{variantName.ToLowerInvariant()}");
    }
}
