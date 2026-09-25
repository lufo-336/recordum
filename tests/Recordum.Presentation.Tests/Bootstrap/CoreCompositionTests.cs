using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit.Abstractions;

namespace Recordum.Presentation.Tests.Bootstrap;

public sealed class CoreCompositionTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-core-" + Guid.NewGuid().ToString("N"));

    /// <summary>Opzioni puntate a una cartella temporanea. NON <c>RecordumOptions.Default()</c>, che
    /// crea cartelle nel profilo vero dell'utente.</summary>
    private RecordumOptions Options()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "attachments"));
        Directory.CreateDirectory(Path.Combine(_dir, "backups"));
        return new RecordumOptions
        {
            DatabasePath = Path.Combine(_dir, "recordum.db"),
            KeyFilePath = Path.Combine(_dir, "db.key"),
            SaltFilePath = Path.Combine(_dir, "db.salt"),
            AttachmentsDirectory = Path.Combine(_dir, "attachments"),
            BackupsDirectory = Path.Combine(_dir, "backups"),
            AutoBackupKeyPath = Path.Combine(_dir, "autobackup.key"),
        };
    }

    public void Dispose()
    {
        // Le connessioni SQLite sono in pool: senza questo il file resta aperto e la cartella non si cancella.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    [Fact]
    public void Every_service_registered_by_core_resolves_with_shell_doubles()
    {
        var services = new ServiceCollection();
        services.AddRecordumCore(Options());
        var coreDescriptors = services.ToList();          // fotografia PRIMA dei doppi: solo ciò che registra core
        services.AddShellDoubles();
        // Come WPF: BuildServiceProvider() senza validazione degli scope (decisione D9). I ViewModel
        // transient prendono servizi scoped dal root: uno smell già esistente, non un difetto qui.
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = new List<Type>();
        foreach (var d in coreDescriptors)
        {
            if (d.ServiceType.IsGenericTypeDefinition) continue;   // ILogger<>, IOptions<>: non istanziabili da soli
            var instance = d.IsKeyedService
                ? scope.ServiceProvider.GetRequiredKeyedService(d.ServiceType, d.ServiceKey)
                : scope.ServiceProvider.GetRequiredService(d.ServiceType);
            Assert.NotNull(instance);
            resolved.Add(d.ServiceType);
        }

        output.WriteLine($"risolti {resolved.Count} servizi");
        // Non vuoto per costruzione: un loop che non gira passerebbe in silenzio.
        Assert.True(resolved.Count > 100, $"attesi >100 servizi core, trovati {resolved.Count}");
        Assert.Contains(typeof(MainWindowViewModel), resolved);
        Assert.Contains(typeof(HomeViewModel), resolved);
        // Il costruttore a 26 dipendenze che si apre al click su una riga Home.
        Assert.Contains(typeof(HabitScreenViewModel), resolved);
        Assert.Contains(typeof(HabitEditorViewModel), resolved);
        Assert.Contains(typeof(HabitBuilderViewModel), resolved);
    }

    [Fact]
    public void Core_does_not_register_the_platform_services()
    {
        var services = new ServiceCollection();
        services.AddRecordumCore(Options());
        var registered = services.Select(d => d.ServiceType).ToHashSet();

        // Ciò che ogni shell deve fornire da sé. Se core ne registrasse uno, la shell che ne mette la
        // propria versione lo sovrascriverebbe in silenzio (vince l'ultima registrazione) — o peggio,
        // una shell che se lo dimentica avrebbe un default sbagliato invece di un errore di risoluzione.
        Type[] platform =
        [
            typeof(IDbKeyProvider), typeof(IThemeService), typeof(ITickSourceFactory),
            typeof(IApplicationLifetime), typeof(IConfirmationDialog), typeof(IHabitTemplatePickerLauncher),
            typeof(IHabitBuilderLauncher), typeof(ISettingsWindowLauncher), typeof(IExportDialogLauncher),
            typeof(IBackupDialogLauncher), typeof(ISaveFileDialog), typeof(IBackupSaveFileDialog),
            typeof(IOpenFileDialog), typeof(IClipboard), typeof(IScreenColorPicker),
            typeof(IAutoBackupKeyStore), typeof(IEditorAvailability),
        ];
        Assert.All(platform, t => Assert.DoesNotContain(t, registered));
    }

    [Fact]
    public void Without_the_shell_doubles_the_graph_does_not_resolve()
    {
        // La prova che il test di sopra non è verde per costruzione: tolti i doppi, MainWindowViewModel
        // (che dipende da IApplicationLifetime, ISettingsWindowLauncher…) non si risolve.
        var services = new ServiceCollection();
        services.AddRecordumCore(Options());
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MainWindowViewModel>());
    }

    [Fact]
    public async Task CoreInitializer_migrates_an_encrypted_database_and_runs_the_startup_steps()
    {
        var options = Options();
        var key = new byte[KeyMaterial.LengthBytes];
        for (var i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);
        var steps = new SpyStartupStep();
        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddShellDoubles(new FixedKeyProvider(key));
        services.AddSingleton<IStartupStep>(steps);
        await using var provider = services.BuildServiceProvider();

        var started = System.Diagnostics.Stopwatch.StartNew();
        await provider.GetRequiredService<CoreInitializer>().InitializeAsync();
        output.WriteLine($"CoreInitializer su DB nuovo: {started.ElapsedMilliseconds} ms");

        // Il file esiste ed è CIFRATO: l'header in chiaro di SQLite non c'è. (Regressione SEC-1:
        // se il provider SQLite in chiaro vincesse, PRAGMA key sarebbe un no-op e qui leggeremmo
        // "SQLite format 3\0".)
        Assert.True(File.Exists(options.DatabasePath));
        var header = new byte[16];
        // FileShare.ReadWrite: SQLite tiene il file aperto in scrittura, e su Windows File.OpenRead (FileShare.Read)
        // fallisce con "used by another process" — su Linux la condivisione non è obbligatoria e passava.
        await using (var fs = new FileStream(options.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            _ = await fs.ReadAsync(header);
        Assert.NotEqual("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(header));

        await using var scope = provider.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IAppSettingService>();
        Assert.Equal("2.18", await settings.GetAsync("schema_version"));       // MigrateAsync ha girato
        Assert.Equal("true", await settings.GetAsync("maintenance.retro_noon_backfill_done")); // il backfill R9 ha girato
        Assert.Equal(1, steps.Calls);                                          // gli step opzionali, una volta
    }

    [Fact]
    public async Task CoreInitializer_fails_startup_when_the_key_is_wrong()
    {
        var options = Options();
        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddShellDoubles(new FixedKeyProvider(new byte[KeyMaterial.LengthBytes]));
        await using (var provider = services.BuildServiceProvider())
            await provider.GetRequiredService<CoreInitializer>().InitializeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var wrong = new byte[KeyMaterial.LengthBytes];
        wrong[0] = 0xFF;
        var services2 = new ServiceCollection();
        services2.AddRecordumCore(options);
        services2.AddShellDoubles(new FixedKeyProvider(wrong));
        await using var provider2 = services2.BuildServiceProvider();

        // Nessun try/catch nell'initializer: una chiave sbagliata deve far fallire l'avvio, non
        // produrre un'app che parte su un DB che non può leggere.
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => provider2.GetRequiredService<CoreInitializer>().InitializeAsync());
    }

    private sealed class SpyStartupStep : IStartupStep
    {
        public int Calls { get; private set; }

        public Task RunAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
