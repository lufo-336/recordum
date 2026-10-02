using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Theming;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;
using Recordum.UI.Views;
using Recordum.UI.Views.Startup;

namespace Recordum.UI;

public partial class App : Application
{
    private ServiceProvider? _services;
    private DataDirectoryLock? _dataLock;   // errata 2026-09-30: un'istanza sola per cartella dati

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Solo con un lifetime desktop classico. Sotto il lifetime headless dei test questo ramo
        // non si apre, quindi nessun [AvaloniaFact] apre database o finestre di sblocco (D14).
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Esplicito, e prima di qualunque finestra: chiudere la finestra di sblocco non deve
            // spegnere l'applicazione. Torna a OnMainWindowClose quando la finestra vera esiste.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) =>
            {
                // finally: il lock della cartella dati si rilascia anche se la chiusura del contenitore lancia.
                try { _services?.Dispose(); }
                finally { _dataLock?.Dispose(); }
            };
            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var args = desktop.Args ?? [];

        // --data-dir / RECORDUM_DATA_DIR (errata 2026-09-30). Prima del log: il log vive nella cartella dei
        // dati scelta, cosi' un'esecuzione con --data-dir non scrive nella cartella vera dell'utente.
        RecordumOptions options;
        try
        {
            options = RecordumOptions.Resolve(args, Environment.GetEnvironmentVariable);
        }
        catch (Exception ex)
        {
            // Senza opzioni non si sa dove scrivere: si ripiega sul percorso di default.
            CrashLogger.WireUp();
            CrashLogger.Log("App.StartAsync (opzioni)", ex);
            ShowFatal(desktop, FormatStartupError(ex));
            return;
        }

        CrashLogger.WireUp(Path.Combine(options.DataDirectory, "logs"));
        try
        {
            // Il lock prima di toccare salt o database.
            _dataLock = DataDirectoryLock.TryAcquire(options.DataDirectory);
            if (_dataLock is null)
            {
                ShowFatal(desktop, Strings.Startup_AlreadyRunning);
                return;
            }

            // I dati finti non finiscono mai in quelli veri: lo si dice PRIMA dello sblocco, altrimenti al primo avvio
            // l'utente crea la passphrase e il database veri e poi riceve un errore fatale (review F1, m4).
            if (SeedRefusedOnRealData(args, options))
            {
                ShowFatal(desktop, FormatStartupError(new InvalidOperationException(DemoDataSeeder.RealDataRefusal)));
                return;
            }

            var keyProvider = await ResolveKeyAsync(desktop, options).ConfigureAwait(true);
            if (keyProvider is null) return;   // sblocco annullato o non supportato: si e' gia' detto perche'

            _services = AppComposition.BuildServices(options, keyProvider);
            await _services.GetRequiredService<CoreInitializer>().InitializeAsync().ConfigureAwait(true);

            // Strumento di sviluppo, come in WPF: popola una libreria credibile (idempotente).
            if (args.Contains("--seed-demo"))
                await _services.GetRequiredService<DemoDataSeeder>().SeedAsync().ConfigureAwait(true);
            var seedHabitIndex = Array.IndexOf(args, "--seed-habit");
            if (seedHabitIndex >= 0 && seedHabitIndex + 1 < args.Length)
                await _services.GetRequiredService<DemoDataSeeder>()
                    .SeedHabitAsync(args[seedHabitIndex + 1]).ConfigureAwait(true);

            // Prima della prima finestra e dopo l'inizializzazione: le scelte vivono negli AppSetting.
            var culture = _services.GetRequiredService<ICultureService>();
            await culture.InitializeAsync().ConfigureAwait(true);
            CultureSetup.Apply(culture.Format, culture.Language);

            var theme = _services.GetRequiredService<IThemeService>();
            await theme.InitializeAsync().ConfigureAwait(true);

            var window = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("App.StartAsync (fatale)", ex);
            ShowFatal(desktop, FormatStartupError(ex));
        }
    }

    /// <summary>Gli stessi flag e la stessa regola di <c>DemoDataSeeder.EnsureNotRealData</c>, valutati prima dello sblocco.</summary>
    internal static bool SeedRefusedOnRealData(string[] args, RecordumOptions options) =>
        options.IsDefaultLocation && (args.Contains("--seed-demo") || args.Contains("--seed-habit"));

    // Letto al momento dell'uso, non in un campo statico: la cultura dell'app e' impostata dopo l'avvio.
#pragma warning disable CA1863 // una volta per processo, e il formato non si puo' cachare prima della cultura
    private static string FormatStartupError(Exception ex) =>
        string.Format(CultureInfo.CurrentCulture, Strings.StartupErrorMessage, ex.Message);
#pragma warning restore CA1863

    /// <summary>Rileva lo stato del database e ne ricava la chiave: niente su Windows con DPAPI, una
    /// finestra di sblocco dove serve una passphrase. Torna null quando non si puo' proseguire — e in
    /// quel caso ha gia' mostrato il perche'.</summary>
    private static async Task<IDbKeyProvider?> ResolveKeyAsync(
        IClassicDesktopStyleApplicationLifetime desktop, RecordumOptions options)
    {
        var unlock = AppComposition.CreateUnlockService(options);
        unlock.ReconcileOrphans();   // errata 2026-09-30: un salt senza database si recupera o si sposta, mai si tronca
        var state = unlock.DetectState();

        if (state == DbUnlockState.Unsupported)
        {
            ShowFatal(desktop, Strings.Unlock_Unsupported);
            return null;
        }

        if (state == DbUnlockState.UsePlatformKey)
        {
            var result = await unlock.UnlockAsync(state, passphrase: string.Empty).ConfigureAwait(true);
            if (result.Outcome == UnlockOutcome.Unlocked && result.Provider is not null) return result.Provider;
            ShowFatal(desktop, Strings.Unlock_Failed);
            return null;
        }

        var viewModel = new UnlockViewModel(unlock, state);
        var window = new UnlockWindow(viewModel);
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();

        // Chi arriva prima: lo sblocco riuscito, o l'utente che chiude la finestra.
        var winner = await Task.WhenAny(viewModel.Result, closed.Task).ConfigureAwait(true);
        if (winner != viewModel.Result)
        {
            desktop.Shutdown();
            return null;
        }
        var provider = await viewModel.Result.ConfigureAwait(true);
        window.Close();
        return provider;
    }

    private static void ShowFatal(IClassicDesktopStyleApplicationLifetime desktop, string message)
    {
        var window = new FatalErrorWindow(message, CrashLogger.LogPath);
        window.Closed += (_, _) => desktop.Shutdown(1);
        window.Show();
    }
}
