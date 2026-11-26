using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Bootstrap;

namespace Recordum.UI.Tests.Fixtures;

/// <summary>Una testa per i test: i default rispondono come quella del computer (DesktopHead), ogni proprieta' si
/// cambia per provare l'altro ramo (il "parametro muto"). Le registrazioni di shell sono quelle desktop.</summary>
public sealed class FakeAppHead : IAppHead
{
    public IReadOnlyList<string> Args { get; init; } = [];
    public RecordumOptions? Options { get; init; }
    public RememberUnavailableReason MissingKeyringReason { get; init; } =
        OperatingSystem.IsLinux() ? RememberUnavailableReason.NoKeyring : RememberUnavailableReason.NotSupported;
    public TimeSpan StoredKeyCap { get; init; } = StoredKeyUnlock.DefaultCap;
    public bool UsesWindowsDpapi { get; init; } = OperatingSystem.IsWindows();
    public HomeRows DefaultHomeRows { get; init; } = HomeRows.Comfortable;
    public bool RunsSync { get; init; } = true;
    public int ShellServicesCalls { get; private set; }
    public List<string> Reported { get; } = [];

    public RecordumOptions ResolveOptions() => Options ?? throw new InvalidOperationException("FakeAppHead senza Options");
    public ISecretStore CreateSecretStore(RecordumOptions options) => new NullSecretStore();

    public void AddShellServices(IServiceCollection services)
    {
        ShellServicesCalls++;
        services.AddRecordumDesktopShell();
    }

    public TimeSpan ElapsedSinceProcessStart() => TimeSpan.Zero;
    public void Report(string line) => Reported.Add(line);
}
