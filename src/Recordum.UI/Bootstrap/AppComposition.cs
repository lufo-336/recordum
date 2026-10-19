using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.UI.Diagnostics;
using Recordum.UI.Views;

namespace Recordum.UI.Bootstrap;

/// <summary>La composizione della shell Avalonia, in un solo posto e senza toccare
/// <see cref="Avalonia.Application"/>.
///
/// <para>Perche' e' un metodo statico e non il corpo di <c>App.OnFrameworkInitializationCompleted</c>:
/// i test d'interfaccia costruiscono la <b>vera</b> <c>App</c> (<c>AppBuilder.Configure&lt;App&gt;()</c>
/// in <c>TestAppBuilder</c>), quindi qualunque cosa <c>App</c> faccia all'avvio la farebbe ogni
/// <c>[AvaloniaFact]</c> — aprendo un database e una finestra di sblocco per ogni test. <c>App</c>
/// percorre la sua strada solo con un lifetime desktop classico; i test chiamano questo metodo con le
/// loro opzioni e la loro chiave (D14).</para></summary>
public static class AppComposition
{
    public static ServiceProvider BuildServices(RecordumOptions options, IDbKeyProvider keyProvider, ISecretStore secrets,
        Action<IServiceCollection>? configure = null) =>
        BuildServices(options, keyProvider, secrets, OperatingSystem.IsWindows(), configure);

    /// <summary><paramref name="secrets"/> e' la STESSA istanza che ha servito lo sblocco (App la costruisce una volta);
    /// <paramref name="windows"/> sceglie il deposito della passphrase dell'auto-backup, ed e' un parametro per poter
    /// provare l'altro ramo su qualunque sistema.</summary>
    public static ServiceProvider BuildServices(RecordumOptions options, IDbKeyProvider keyProvider, ISecretStore secrets, bool windows,
        Action<IServiceCollection>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(keyProvider);
        ArgumentNullException.ThrowIfNull(secrets);

        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddRecordumAvaloniaShell();
        // Dopo le due estensioni, cosi' la chiave decisa dallo sblocco vince su qualunque default.
        services.AddSingleton(keyProvider);
        AddBackupKeyStores(services, options, secrets, windows);
        services.AddTransient<MainWindow>();
        configure?.Invoke(services);   // solo i test: l'ultima registrazione vince; App non passa niente

        // BuildServiceProvider() SENZA validazione degli scope, come la shell WPF: i ViewModel
        // transient prendono servizi scoped dal contenitore radice. E' una captive dependency che
        // esiste gia' (i tab veri ricevono uno scope proprio da ISubjectScreenFactory); accenderla
        // qui farebbe fallire la risoluzione di ogni ViewModel della Home. Annotata a tracker (D9),
        // non risolta da F1.
        return services.BuildServiceProvider();
    }

    /// <summary>Dove vive la passphrase dell'auto-backup: DPAPI su Windows (come WPF), il portachiavi altrove.</summary>
    public static void AddBackupKeyStores(IServiceCollection services, RecordumOptions options, ISecretStore secrets, bool windows)
    {
        services.AddSingleton(secrets);
#pragma warning disable CA1416 // il ramo Windows si sceglie con un parametro (provabile ovunque); DPAPI si usa solo se windows e' vero
        if (windows) services.AddSingleton<IAutoBackupKeyStore>(_ => new DpapiAutoBackupKeyStore(options.AutoBackupKeyPath));
#pragma warning restore CA1416
        else services.AddSingleton<IAutoBackupKeyStore>(sp => new SecretStoreAutoBackupKeyStore(sp.GetRequiredService<ISecretStore>()));
    }

    /// <summary>L'avvio della shell: <c>CoreInitializer</c>, poi lo scheduler dell'auto-backup. Un solo punto, cosi' un
    /// test passa dallo stesso percorso di <c>App</c>.</summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await services.GetRequiredService<CoreInitializer>().InitializeAsync().ConfigureAwait(true);
        StartBackupScheduler(services);
    }

    /// <summary>Avvia lo scheduler dell'auto-backup: dopo <c>CoreInitializer</c> (il DB e' migrato), con l'attesa del
    /// primo tick, nessun backup immediato. Il ciclo gira su un thread del pool (<c>Task.Run</c>), quindi la lettura
    /// della passphrase dal portachiavi non tocca mai il thread dell'interfaccia. Si ferma con il Dispose del contenitore.</summary>
    public static void StartBackupScheduler(IServiceProvider services) =>
        services.GetRequiredService<BackupSchedulerHost>().Initialize();

    /// <summary>Il servizio di sblocco che gira prima del contenitore. Il logger scrive nel log di
    /// <see cref="CrashLogger"/>: la causa di uno sblocco fallito (disco, permessi, salt rovinato) non
    /// si vede dalla finestra, e senza questo andrebbe persa.</summary>
    public static IDbUnlockService CreateUnlockService(RecordumOptions options, ISecretStore secrets)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);
        return new DbUnlockService(options, secrets, new CrashLoggerAdapter<DbUnlockService>());
    }

    /// <summary>Il portachiavi per sistema: libsecret su Linux, nessuno altrove (su Windows resta DPAPI). Le operazioni
    /// sullo stesso segreto sono in fila (F2c-4): un "Dimentica" non e' annullato da una scrittura dello sblocco tardiva.</summary>
    public static ISecretStore CreateSecretStore(RecordumOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return OperatingSystem.IsLinux() ? new SerializedSecretStore(new LibsecretSecretStore(options.DataDirectory)) : new NullSecretStore();
    }
}
