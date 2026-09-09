using Avalonia;
using Avalonia.Headless;
using Recordum.Avalonia;

[assembly: AvaloniaTestApplication(typeof(Recordum.Avalonia.Tests.TestAppBuilder))]

namespace Recordum.Avalonia.Tests;

public static class TestAppBuilder
{
    // Nota: NON UsePlatformDetect() — headless è il "platform".
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
