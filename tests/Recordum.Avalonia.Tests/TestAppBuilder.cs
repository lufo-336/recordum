using Avalonia;
using Avalonia.Headless;
using Recordum.Avalonia;

[assembly: AvaloniaTestApplication(typeof(Recordum.Avalonia.Tests.TestAppBuilder))]

namespace Recordum.Avalonia.Tests;

public static class TestAppBuilder
{
    // Nota: NON UsePlatformDetect() — headless è il "platform".
    // UseSkia() + UseHeadlessDrawing = false: tentativo di fallback della sonda screenshot
    // (Task 2, ScreenshotProbeTests) — CaptureRenderedFrame torna null col disegno finto
    // di default, quindi si prova a far disegnare Skia davvero anche in headless.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
