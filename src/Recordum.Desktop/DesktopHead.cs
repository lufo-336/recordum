using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;

namespace Recordum.Desktop;

/// <summary>La testa del computer (Windows e Linux): opzioni da riga di comando e variabile d'ambiente,
/// portachiavi di sistema (libsecret su Linux, DPAPI su Windows) e shell desktop a finestre.</summary>
public sealed class DesktopHead(IReadOnlyList<string> args) : IAppHead
{
    private readonly long _start = Stopwatch.GetTimestamp();

    public IReadOnlyList<string> Args { get; } = args;

    public RecordumOptions ResolveOptions() =>
        RecordumOptions.Resolve([.. Args], Environment.GetEnvironmentVariable);

    public ISecretStore CreateSecretStore(RecordumOptions options) =>
        AppComposition.CreateSecretStore(options);

    // Senza un portachiavi che risponde: su Linux manca il Secret Service, altrove Recordum non ne usa uno.
    public RememberUnavailableReason MissingKeyringReason =>
        OperatingSystem.IsLinux() ? RememberUnavailableReason.NoKeyring : RememberUnavailableReason.NotSupported;

    public TimeSpan StoredKeyCap => StoredKeyUnlock.DefaultCap;

    public bool UsesWindowsDpapi => OperatingSystem.IsWindows();

    public HomeRows DefaultHomeRows => HomeRows.Comfortable;

    public bool RunsSync => true;

    public void AddShellServices(IServiceCollection services) =>
        services.AddRecordumDesktopShell();

    public TimeSpan ElapsedSinceProcessStart() => Stopwatch.GetElapsedTime(_start);

    public void Report(string line) => CrashLogger.Info(line);
}
