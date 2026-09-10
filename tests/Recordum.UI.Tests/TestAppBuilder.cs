using Avalonia;
using Avalonia.Headless;
using Recordum.UI;

[assembly: AvaloniaTestApplication(typeof(Recordum.UI.Tests.TestAppBuilder))]

namespace Recordum.UI.Tests;

public static class TestAppBuilder
{
    // Nota: NON UsePlatformDetect() — headless è il "platform".
    //
    // UseSkia() + UseHeadlessDrawing = false: col disegno finto di default
    // CaptureRenderedFrame() torna null, quindi si fa disegnare Skia davvero anche in headless.
    //
    // ATTENZIONE, raggio d'azione: questo file porta [assembly: AvaloniaTestApplication], quindi la
    // scelta vale per OGNI [AvaloniaFact] di questo progetto, presenti e futuri — non solo per la
    // sonda screenshot. Ogni test headless renderizza per davvero, il che costa un po' di più e
    // dipende dai nativi di Skia (che arrivano transitivamente da Avalonia.Desktop, non da un
    // PackageReference nostro). Non tornare al disegno finto senza prima verificare
    // ScreenshotProbeTests.Capture: gli screenshot smetterebbero di funzionare in silenzio.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
