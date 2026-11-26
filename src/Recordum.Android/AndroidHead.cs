using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;

namespace Recordum.Android;

/// <summary>La testa del telefono: cartella dati privata dell'app, portachiavi (Keystore al Task 10) e shell mobile.</summary>
public sealed class AndroidHead : IAppHead
{
    public IReadOnlyList<string> Args => [];

    // La cartella predefinita: su Android LocalApplicationData sta dentro FilesDir (privata dell'app), e così
    // IsDefaultLocation è vero — sono i dati veri, --seed-demo li rifiuta (review Argon del Task 4).
    public RecordumOptions ResolveOptions() => RecordumOptions.Default();

    // Keystore al Task 10.
    public ISecretStore CreateSecretStore(RecordumOptions options) => new NullSecretStore();

    public RememberUnavailableReason MissingKeyringReason => RememberUnavailableReason.NotSupported;

    public TimeSpan StoredKeyCap => Timeout.InfiniteTimeSpan;

    public bool UsesWindowsDpapi => false;

    public HomeRows DefaultHomeRows => HomeRows.Compact;

    public bool RunsSync => false;

    public void AddShellServices(IServiceCollection services)
    {
        // Task 5: AddRecordumPhoneShell
        services.AddRecordumDesktopShell();
        services.AddSingleton<IApplicationLifetime, AndroidApplicationLifetime>();
    }

    public TimeSpan ElapsedSinceProcessStart() =>
        TimeSpan.FromMilliseconds(global::Android.OS.SystemClock.ElapsedRealtime() - global::Android.OS.Process.StartElapsedRealtime);

    public void Report(string line)
    {
        _ = global::Android.Util.Log.Info("Recordum", line);
        CrashLogger.Info(line);
    }
}
