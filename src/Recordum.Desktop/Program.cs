using Avalonia;
using Recordum.UI;

namespace Recordum.Desktop;

internal static class Program
{
    // Avalonia vuole che nulla tocchi la UI prima di AppMain.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp(args).StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp(string[] args) =>
        AppBuilder.Configure(() => new App { Head = new DesktopHead(args) })
            .UsePlatformDetect()
            // Lega la finestra a recordum.desktop (icona e nome nella shell): scripts/install-linux.sh.
            .With(new X11PlatformOptions { WmClass = "recordum" })
            .LogToTrace();
}
