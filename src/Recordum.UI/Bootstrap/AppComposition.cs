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
    public static ServiceProvider BuildServices(RecordumOptions options, IDbKeyProvider keyProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(keyProvider);

        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddRecordumAvaloniaShell();
        // Dopo le due estensioni, cosi' la chiave decisa dallo sblocco vince su qualunque default.
        services.AddSingleton(keyProvider);
        services.AddTransient<MainWindow>();

        // BuildServiceProvider() SENZA validazione degli scope, come la shell WPF: i ViewModel
        // transient prendono servizi scoped dal contenitore radice. E' una captive dependency che
        // esiste gia' (i tab veri ricevono uno scope proprio da ISubjectScreenFactory); accenderla
        // qui farebbe fallire la risoluzione di ogni ViewModel della Home. Annotata a tracker (D9),
        // non risolta da F1.
        return services.BuildServiceProvider();
    }

    /// <summary>Il servizio di sblocco che gira prima del contenitore. Il logger scrive nel log di
    /// <see cref="CrashLogger"/>: la causa di uno sblocco fallito (disco, permessi, salt rovinato) non
    /// si vede dalla finestra, e senza questo andrebbe persa.</summary>
    public static IDbUnlockService CreateUnlockService(RecordumOptions options, ISecretStore secrets)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);
        return new DbUnlockService(options, secrets, new CrashLoggerAdapter<DbUnlockService>());
    }

    /// <summary>Il portachiavi per sistema: libsecret su Linux, nessuno altrove (su Windows resta DPAPI).</summary>
    public static ISecretStore CreateSecretStore(RecordumOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return OperatingSystem.IsLinux() ? new LibsecretSecretStore(options.DataDirectory) : new NullSecretStore();
    }
}
