using Avalonia;

namespace Recordum.UI;

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
            // Lega la finestra a recordum.desktop (icona e nome nella shell): scripts/install-linux.sh.
            .With(new X11PlatformOptions { WmClass = "recordum" })
            .LogToTrace();
}
