using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;

namespace Recordum.UI;

public partial class App : Application
{
    /// <summary>La testa della piattaforma (Desktop, Android). Se null l'avvio non parte:
    /// i test headless costruiscono <c>App</c> senza testa.</summary>
    public IAppHead? Head { get; init; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Solo con una testa e un lifetime conosciuto. Sotto il lifetime headless dei test questo ramo
        // non si apre, quindi nessun [AvaloniaFact] apre database o finestre di sblocco (D14).
        if (Head is { } head)
        {
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    // Esplicito, e prima di qualunque finestra: chiudere la finestra di sblocco non deve
                    // spegnere l'applicazione. Torna a OnMainWindowClose quando la finestra vera esiste.
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    // Non un campo: l'Exit la chiude, e App non e' IDisposable (CA1001).
                    var startup = new AppStartup(head, new DesktopStartupHost(desktop));
                    desktop.Exit += (_, _) =>
                    {
                        try
                        {
                            startup.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(12));
                        }
                        catch (Exception ex)
                        {
                            CrashLogger.Log("App.Exit", ex);
                        }
                    };
                    _ = startup.RunAsync();
                    break;

                case ISingleViewApplicationLifetime:
                    throw new NotSupportedException("PhoneStartupHost arriva col Task 5");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
