using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Data;
using Recordum.Data.Maintenance;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Presentation.Bootstrap;

/// <summary>L'avvio portabile: la metà di <c>ApplicationInitializer</c> (WPF) che non nomina né
/// WebView2 né i motori dei plugin. Migra il database, esegue il backfill R9 e tocca il plugin
/// manager; poi lascia alla shell i suoi <see cref="IStartupStep"/>.
/// <para><b>Non</b> avvia <c>BackupSchedulerHost</c>: lo scheduler legge la chiave dell'auto-backup
/// con DPAPI (F6). <b>Non</b> "innesca" il file con una connessione prima delle migrazioni come
/// faceva WPF: quel passo dichiarava di far fallire subito una chiave sbagliata, ma
/// <c>PRAGMA key</c> non verifica nulla (misurato) — l'errore arriva alla prima lettura, cioè dalla
/// <c>MigrateAsync</c> qui sotto, ed è quel fallimento che ferma l'avvio. Lo sblocco interattivo,
/// che invece DEVE offrire un nuovo tentativo, sta in <c>IDbUnlockService</c>, prima di questo.</para></summary>
public sealed class CoreInitializer(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IServiceScopeFactory scopes,
    IPluginManager pluginManager,
    IEnumerable<IStartupStep> steps)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
            await db.Database.MigrateAsync(ct).ConfigureAwait(false);

        // Manutenzione una tantum R9. Dopo le migrazioni (serve lo schema aggiornato), prima di
        // tutto ciò che legge giorni. Nessun try/catch: se fallisce deve fallire l'avvio come una
        // migrazione fallita. Uno scope proprio: il backfill è registrato scoped, e chiederlo al
        // root provider lo renderebbe una dipendenza catturata (lo smell che WPF ha ancora).
        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RetroactiveNoonBackfill>().RunAsync(ct).ConfigureAwait(false);

        _ = pluginManager.ListEnabled();

        foreach (var step in steps)
            await step.RunAsync(ct).ConfigureAwait(false);
    }
}
