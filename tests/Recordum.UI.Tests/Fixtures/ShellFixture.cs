using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Bootstrap;
using Recordum.UI.Views;

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

        // La STESSA composizione che usa l'applicazione vera (D14): se questo contenitore risolve,
        // risolve anche quello di produzione. Cambiare qui e' la scorciatoia che tradirebbe il test.
        var provider = AppComposition.BuildServices(options, new FixedKeyProvider());
        await provider.GetRequiredService<CoreInitializer>().InitializeAsync();

        // Il processo di test non ha un "fine fixture" per un singleton statico: a ProcessExit si chiude il
        // contenitore e si toglie la cartella, altrimenti ogni esecuzione lascia un recordum-ui-<guid> in /tmp.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                provider.Dispose();
            }
#pragma warning disable CA1031 // un gestore di ProcessExit che lancia puo' far cadere l'host di test: tutto best effort
            catch (Exception)
            {
                // Il Dispose lancia davvero (singleton con affinita' al thread UI, InvalidOperationException): non
                // deve impedire di togliere la cartella.
            }
            try
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch (Exception)
            {
                // best effort: a fine processo non c'e' nessuno a cui dirlo
            }
#pragma warning restore CA1031
        };
        return new ShellFixture { Services = provider, Options = options };
    }

    /// <summary>Quante abitudini/gruppi la semina mette nel database.</summary>
    public const int SeededNodeCount = 4;

    private bool _seeded;

    /// <summary>Semina una libreria minima con i SERVIZI veri (niente INSERT a mano): un gruppo con
    /// due figli piu' un'abitudine di primo livello. Idempotente: la fixture e' condivisa.</summary>
    public async Task SeedAsync()
    {
        if (_seeded) return;
        _seeded = true;
        using var scope = Services.CreateScope();
        var nodes = scope.ServiceProvider.GetRequiredService<IHabitNodeService>();
        var groupId = await nodes.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Group, Name = "Salute", Color = "#16A34A",
        });
        await nodes.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Action, ParentId = groupId,
            Name = "Corsa", Color = "#3B82F6",
        });
        await nodes.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Counter, ParentId = groupId,
            Name = "Acqua", Color = "#0EA5E9",
        });
        await nodes.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Journal,
            Name = "Diario", Color = "#A855F7",
        });
    }

    public HomeRowViewModel NewHomeRow(string name) => new(
        new HomeTreeRow(Guid.NewGuid(), null, name, "#3B82F6", HabitType.Action, null, 0, false,
            PinScope.None, 0, false, [], []),
        indentLevel: 0,
        Services.GetRequiredService<IHabitNodeService>(),
        Services.GetRequiredService<IPurgeService>());

    /// <summary>Il DB e' condiviso da tutti i test: un test che commuta la sidebar o apre un tab lascia lo stato
    /// del workspace persistito, e ogni <c>MainWindow</c> successiva lo ripristina (F1 review I1: sidebar a larghezza 0).
    /// Il ViewModel nuovo parte quindi sempre da uno stato pulito.</summary>
    public MainWindowViewModel NewMainWindowViewModel()
    {
        // Sul pool: il servizio e' scoped e la scrittura non deve incrociare il contesto di sincronizzazione del thread UI.
        Task.Run(async () =>
        {
            using var scope = Services.CreateScope();
            // "ui.workspace" = WorkspaceViewModel.StateSettingKey (internal in Presentation).
            await scope.ServiceProvider.GetRequiredService<IAppSettingService>()
                .SetAsync("ui.workspace", new WorkspaceState().ToJson());
        }).GetAwaiter().GetResult();
        return Services.GetRequiredService<MainWindowViewModel>();
    }

    /// <summary>Apre una <c>MainWindow</c> e fa girare il ciclo finche' <c>InitializeAsync</c> (carico della Home +
    /// ripristino dello stato) non e' finito: senza, il ripristino scatta a meta' test.</summary>
    public static async Task<(MainWindow Window, MainWindowViewModel Vm)> OpenMainWindowAsync(MainWindowViewModel vm)
    {
        var window = new MainWindow(vm);
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 400 && !window.Initialization.IsCompleted; i++)
        {
            await Task.Delay(25);
            Dispatcher.UIThread.RunJobs();
        }
        await window.Initialization;   // propaga un'eventuale eccezione; e se non e' partita, e' gia' completata
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }
    public WorkspaceViewModel NewWorkspaceViewModel() => Services.GetRequiredService<WorkspaceViewModel>();

    private sealed class FixedKeyProvider : IDbKeyProvider
    {
        private readonly byte[] _key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1)).ToArray();
        public string ProviderId => "fixed-test-key";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
            => ValueTask.FromResult(new KeyMaterial([.. _key]));
    }
}
