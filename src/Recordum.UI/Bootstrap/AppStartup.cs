using System.Globalization;
using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Theming;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Controls;
using Recordum.UI.Diagnostics;

namespace Recordum.UI.Bootstrap;

/// <summary>La sequenza d'avvio condivisa fra desktop e telefono: esegue i passi lenti fuori dal thread UI,
/// porta solo le viste e la configurazione dell'interfaccia sul <see cref="Dispatcher.UIThread"/>, e misura i tempi.</summary>
public sealed class AppStartup : IAsyncDisposable
{
    private readonly IAppHead _head;
    private readonly IStartupHost _host;
    private readonly Action<IServiceCollection>? _configure;
    private Task<ServiceProvider?>? _run;
    private ServiceProvider? _services;
    private DataDirectoryLock? _dataLock;   // errata 2026-09-30: un'istanza sola per cartella dati

    public AppStartup(IAppHead head, IStartupHost host) : this(head, host, null) { }

    // Solo per i test: registrazioni in più passate a AppComposition.BuildServices(…, configure).
    internal AppStartup(IAppHead head, IStartupHost host, Action<IServiceCollection>? configure)
    {
        _head = head ?? throw new ArgumentNullException(nameof(head));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _configure = configure;
        Timeline = new StartupTimeline(_head.ElapsedSinceProcessStart);
    }

    public StartupTimeline Timeline { get; }

    public Task<ServiceProvider?> RunAsync() => _run ??= RunCoreAsync();

    public ValueTask DisposeAsync()
    {
        var services = Interlocked.Exchange(ref _services, null);
        var dataLock = Interlocked.Exchange(ref _dataLock, null);
        try
        {
            // Limitato a 10 s: la chiusura non resta appesa a una cartella di scambio lenta. Il giro gira sul
            // pool (ConfigureAwait(false)), quindi attenderlo qui non puo' bloccarsi sul thread dell'interfaccia.
            using var flushCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            services?.GetService<SyncSchedulerHost>()?.FlushAsync(flushCts.Token).Wait(TimeSpan.FromSeconds(11));
        }
        catch (Exception ex)
        {
            CrashLogger.Log("AppStartup.DisposeAsync (sync flush)", ex);
        }
        finally
        {
            try { services?.Dispose(); }
            finally { dataLock?.Dispose(); }
        }
        return ValueTask.CompletedTask;
    }

    private async Task<ServiceProvider?> RunCoreAsync()
    {
        string[] args = [.. _head.Args];

        // --data-dir / RECORDUM_DATA_DIR (errata 2026-09-30). Prima del log: il log vive nella cartella dei
        // dati scelta, cosi' un'esecuzione con --data-dir non scrive nella cartella vera dell'utente.
        RecordumOptions options;
        try
        {
            options = _head.ResolveOptions();
        }
        catch (Exception ex)
        {
            // Senza opzioni non si sa dove scrivere: si ripiega sul percorso di default.
            CrashLogger.WireUp();
            CrashLogger.Log("AppStartup (opzioni)", ex);
            await ShowFatalAsync(FormatStartupError(ex), CrashLogger.LogPath).ConfigureAwait(false);
            return null;
        }

        CrashLogger.WireUp(Path.Combine(options.DataDirectory, "logs"));
        await ShowWaitingAsync(Strings.Startup_Waiting_Opening).ConfigureAwait(false);
        Timeline.Mark("options");

        try
        {
            // Il lock prima di toccare salt o database.
            _dataLock = await Task.Run(() => DataDirectoryLock.TryAcquire(options.DataDirectory)).ConfigureAwait(false);
            if (_dataLock is null)
            {
                await ShowFatalAsync(Strings.Startup_AlreadyRunning, CrashLogger.LogPath).ConfigureAwait(false);
                return null;
            }
            Timeline.Mark("lock");

            // I dati finti non finiscono mai in quelli veri: lo si dice PRIMA dello sblocco, altrimenti al primo avvio
            // l'utente crea la passphrase e il database veri e poi riceve un errore fatale (review F1, m4).
            if (SeedRefusedOnRealData(args, options))
            {
                await ShowFatalAsync(FormatStartupError(new InvalidOperationException(DemoDataSeeder.RealDataRefusal)), CrashLogger.LogPath)
                    .ConfigureAwait(false);
                return null;
            }
            Timeline.Mark("seed-refusal");

            // errata 2026-09-30: un salt senza database si recupera o si sposta, mai si tronca.
            var (secrets, unlock) = await Task.Run(() =>
            {
                var s = _head.CreateSecretStore(options);   // una volta: serve sia lo sblocco sia il contenitore
                var u = AppComposition.CreateUnlockService(options, s);
                u.ReconcileOrphans();
                return (s, u);
            }).ConfigureAwait(false);
            Timeline.Mark("reconcile");

            var state = await Task.Run(() => unlock.DetectState()).ConfigureAwait(false);
            Timeline.Mark("detect");

            IDbKeyProvider? keyProvider;
            if (state == DbUnlockState.Unsupported)
            {
                await ShowFatalAsync(Strings.Unlock_Unsupported, CrashLogger.LogPath).ConfigureAwait(false);
                return null;
            }
            else if (state == DbUnlockState.UsePlatformKey)
            {
                var result = await Task.Run(() =>
                    unlock.UnlockAsync(state, passphrase: string.Empty, remember: RememberChoice.LeaveAsIs)).ConfigureAwait(false);
                if (result.Outcome != UnlockOutcome.Unlocked || result.Provider is null)
                {
                    await ShowFatalAsync(Strings.Unlock_Failed, CrashLogger.LogPath).ConfigureAwait(false);
                    return null;
                }
                keyProvider = result.Provider;
            }
            else
            {
                // La chiave ricordata (e la sonda del portachiavi) prima della finestra, fuori dal thread UI e con un tetto.
                var stored = await StoredKeyUnlock.RunAsync(unlock, state, _head.StoredKeyCap, _head.MissingKeyringReason)
                    .ConfigureAwait(false);
                if (stored.Provider is not null)
                {
                    keyProvider = stored.Provider;
                }
                else
                {
                    var viewModel = new UnlockViewModel(unlock, state, stored.CanRemember, stored.Rejected, stored.Reason);
                    keyProvider = await UnlockOnUiAsync(viewModel).ConfigureAwait(false);
                    if (keyProvider is null) return null;   // sblocco annullato: l'host ha già spento l'app
                }
            }
            Timeline.Mark("unlock");
            await ShowWaitingAsync(Strings.Startup_Waiting_Preparing).ConfigureAwait(false);

            var services = await Task.Run(() =>
                AppComposition.BuildServices(options, keyProvider, secrets, _head, _configure)).ConfigureAwait(false);
            _services = services;
            Timeline.Mark("container");

            await Task.Run(async () =>
            {
                await services.GetRequiredService<CoreInitializer>().InitializeAsync().ConfigureAwait(false);

                // Strumento di sviluppo: popola una libreria credibile (idempotente).
                if (args.Contains("--seed-demo"))
                    await services.GetRequiredService<DemoDataSeeder>().SeedAsync().ConfigureAwait(false);
                var seedHabitIndex = Array.IndexOf(args, "--seed-habit");
                if (seedHabitIndex >= 0 && seedHabitIndex + 1 < args.Length)
                    await services.GetRequiredService<DemoDataSeeder>()
                        .SeedHabitAsync(args[seedHabitIndex + 1]).ConfigureAwait(false);
            }).ConfigureAwait(false);
            Timeline.Mark("core-init");

            // Prima della prima finestra e dopo l'inizializzazione: le scelte vivono negli AppSetting.
            await RunOnUiAsync(async () =>
            {
                var culture = services.GetRequiredService<ICultureService>();
                await culture.InitializeAsync().ConfigureAwait(true);
                CultureSetup.Apply(culture.Format, culture.Language);

                var theme = services.GetRequiredService<IThemeService>();
                await theme.InitializeAsync().ConfigureAwait(true);

                var homeRows = services.GetRequiredService<HomeRowsPreference>();
                await homeRows.InitializeAsync().ConfigureAwait(true);
                var app = Application.Current!;
                HomeRowsAppStyle.Apply(app, homeRows.Current);
                homeRows.Changed += (_, _) => HomeRowsAppStyle.Apply(app, homeRows.Current);
            }).ConfigureAwait(false);
            Timeline.Mark("culture-theme-rows");

            await ShowMainAsync(services).ConfigureAwait(false);
            Timeline.Mark("home");

            await Task.Run(() => AppComposition.StartBackupScheduler(services)).ConfigureAwait(false);
            Timeline.Mark("backup-scheduler");

            if (_head.RunsSync)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _ = AppComposition.StartSyncSchedulerAsync(services);
                }).GetTask().ConfigureAwait(false);
                Timeline.Mark("sync-scheduler");
            }

            _head.Report(Timeline.Format());
            return services;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("AppStartup (fatale)", ex);
            await ShowFatalAsync(FormatStartupError(ex), CrashLogger.LogPath).ConfigureAwait(false);
            return null;
        }
    }

    private Task ShowWaitingAsync(string status) =>
        Dispatcher.UIThread.InvokeAsync(() => _host.ShowWaiting(status)).GetTask();

    private Task ShowFatalAsync(string message, string? logPath) =>
        Dispatcher.UIThread.InvokeAsync(() => _host.ShowFatal(message, logPath)).GetTask();

    private Task ShowMainAsync(IServiceProvider services) =>
        Dispatcher.UIThread.InvokeAsync(() => _host.ShowMain(services)).GetTask();

    // Avalonia sceglie l'overload Func<Task<T>>: il Task interno si attende per intero sul thread UI.
    private Task<IDbKeyProvider?> UnlockOnUiAsync(UnlockViewModel viewModel) =>
        Dispatcher.UIThread.InvokeAsync(() => _host.UnlockAsync(viewModel));

    private static Task RunOnUiAsync(Func<Task> action) => Dispatcher.UIThread.InvokeAsync(action);

    /// <summary>Gli stessi flag e la stessa regola di <c>DemoDataSeeder.EnsureNotRealData</c>, valutati prima dello sblocco.</summary>
    internal static bool SeedRefusedOnRealData(string[] args, RecordumOptions options) =>
        options.IsDefaultLocation && (args.Contains("--seed-demo") || args.Contains("--seed-habit"));

    // Letto al momento dell'uso, non in un campo statico: la cultura dell'app e' impostata dopo l'avvio.
#pragma warning disable CA1863 // una volta per processo, e il formato non si puo' cachare prima della cultura
    private static string FormatStartupError(Exception ex) =>
        string.Format(CultureInfo.CurrentCulture, Strings.StartupErrorMessage, ex.Message);
#pragma warning restore CA1863
}
