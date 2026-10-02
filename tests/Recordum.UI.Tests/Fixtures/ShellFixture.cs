using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Bootstrap;

namespace Recordum.UI.Tests.Fixtures;

/// <summary>Un contenitore VERO su un DB SQLCipher temporaneo, chiavato da una chiave fissa.</summary>
public sealed class ShellFixture
{
    private static readonly Lazy<ShellFixture> Lazy = new(() => Create().GetAwaiter().GetResult());

    public static ShellFixture Shared => Lazy.Value;

    public required ServiceProvider Services { get; init; }
    public required RecordumOptions Options { get; init; }

    private static async Task<ShellFixture> Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"recordum-ui-{Guid.NewGuid():N}");
        // Errata 2026-09-30: RecordumOptions ha DataDirectory e TempDirectory obbligatori (Passo 0). Resolve crea la
        // cartella con tutte le sottocartelle, con le stesse regole dell'app vera.
        var options = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dir], _ => null);

        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddRecordumAvaloniaShell();
        services.AddSingleton<IDbKeyProvider>(new FixedKeyProvider());
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<CoreInitializer>().InitializeAsync();
        return new ShellFixture { Services = provider, Options = options };
    }

    public HomeRowViewModel NewHomeRow(string name) => new(
        new HomeTreeRow(Guid.NewGuid(), null, name, "#3B82F6", HabitType.Action, null, 0, false,
            PinScope.None, 0, false, [], []),
        indentLevel: 0,
        Services.GetRequiredService<IHabitNodeService>(),
        Services.GetRequiredService<IPurgeService>());

    public MainWindowViewModel NewMainWindowViewModel() => Services.GetRequiredService<MainWindowViewModel>();
    public WorkspaceViewModel NewWorkspaceViewModel() => Services.GetRequiredService<WorkspaceViewModel>();

    private sealed class FixedKeyProvider : IDbKeyProvider
    {
        private readonly byte[] _key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1)).ToArray();
        public string ProviderId => "fixed-test-key";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
            => ValueTask.FromResult(new KeyMaterial([.. _key]));
    }
}
