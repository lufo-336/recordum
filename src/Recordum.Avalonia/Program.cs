using Avalonia;

namespace Recordum.Avalonia;

internal static class Program
{
    // Avalonia vuole che nulla tocchi la UI prima di AppMain.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Chiamato anche dai test headless: DEVE restare public e senza effetti collaterali.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
