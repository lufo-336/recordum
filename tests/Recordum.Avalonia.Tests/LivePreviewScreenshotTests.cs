using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using FluentAssertions;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia.Tests;

/// <summary>
/// Sonda Live Preview (branch avalonia-linux-slice, NON una feature): renderizza
/// <see cref="LivePreviewProbeView"/> — un documento con titolo, grassetto+corsivo e paragrafi
/// piani, col caret piazzato sulla riga formattata — in entrambe le varianti tema e salva i due
/// PNG richiesti dal deliverable #2 del probe. Stesso pattern di
/// <see cref="MarkdownEditorScreenshotTests"/>.
/// </summary>
public class LivePreviewScreenshotTests
{
    [AvaloniaFact]
    public void Live_preview_probe_renders_in_light_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Light, "livepreview-light");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG con titolo reso + riga del caret grezza non può essere pochi byte");
    }

    [AvaloniaFact]
    public void Live_preview_probe_renders_in_dark_variant()
    {
        var path = RenderAndCapture(ThemeVariant.Dark, "livepreview-dark");
        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG con titolo reso + riga del caret grezza non può essere pochi byte");
    }

    private static string RenderAndCapture(ThemeVariant variant, string name)
    {
        // PRIMING (misurato il 2026-09-10): in headless il PRIMO AvaloniaEdit della sessione non
        // fa il proprio passaggio di layout — TextView resta 0x0 e VisualLines lancia
        // VisualLinesInvalidException, quindi il PNG esce con la cornice e senza testo. Dal secondo
        // in poi si dispone regolarmente. Si sacrifica quindi un editor usa-e-getta prima di quello
        // vero. Senza questa riga la sonda "passa" mostrando una schermata vuota: il test peggiore
        // che esista, verde e cieco.
        var priming = new Window { Width = 400, Height = 300, Content = new MarkdownEditorProbeView() };
        priming.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var window = new Window
        {
            Width = 900,
            Height = 560,
            RequestedThemeVariant = variant,
            Content = new LivePreviewProbeView(),
        };
        window.Show();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return ScreenshotProbeTests.Capture(window, name);
    }
}
