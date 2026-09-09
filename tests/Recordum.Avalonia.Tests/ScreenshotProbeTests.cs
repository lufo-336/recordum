using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Recordum.Avalonia.Views;

namespace Recordum.Avalonia.Tests;

public class ScreenshotProbeTests
{
    /// <summary>Salva il frame renderizzato della finestra come PNG e ne torna il percorso.
    /// Serve a poter GUARDARE una schermata invece di asserire soltanto sui ViewModel.</summary>
    internal static string Capture(Window window, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.png");

        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException(
                "CaptureRenderedFrame ha tornato null: il backend headless sta disegnando senza Skia.");
        frame.Save(path);
        return path;
    }

    [AvaloniaFact]
    public void A_rendered_frame_can_be_saved_as_a_png()
    {
        var window = new MainWindow();
        window.Show();

        var path = Capture(window, "probe");

        new FileInfo(path).Length.Should().BeGreaterThan(1000,
            "un PNG di una finestra 1000x700 non può pesare poche centinaia di byte: se è minuscolo, "
            + "il frame è vuoto e la sonda non ha provato niente");
    }
}
