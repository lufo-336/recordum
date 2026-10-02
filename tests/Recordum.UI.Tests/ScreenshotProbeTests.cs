using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;

namespace Recordum.UI.Tests;

public class ScreenshotProbeTests
{
    /// <summary>Salva il frame renderizzato della finestra come PNG e ne torna il percorso.
    /// Serve a poter GUARDARE una schermata invece di asserire soltanto sui ViewModel.
    /// <b>Non</b> asserire sul peso del file: un frame vuoto pesa quanto uno pieno, ed e' cosi' che
    /// una sonda e' gia' passata mostrando una schermata bianca (spec §12).</summary>
    internal static string Capture(Window window, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.png");

        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException(
                "CaptureRenderedFrame ha tornato null: il backend headless sta disegnando senza Skia.");
#pragma warning disable CS0618 // l'overload con BitmapEncoderOptions non esiste in 12.1.2
        frame.Save(path);
#pragma warning restore CS0618
        return path;
    }

    [AvaloniaFact]
    public void The_real_shell_can_be_captured_as_a_png()
    {
        var window = new MainWindow(ShellFixture.Shared.NewMainWindowViewModel());
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var path = Capture(window, "probe");

        File.Exists(path).Should().BeTrue();
    }
}
