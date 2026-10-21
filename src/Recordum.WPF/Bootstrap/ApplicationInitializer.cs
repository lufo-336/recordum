using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Security;
using Recordum.Data;
using Recordum.Data.Maintenance;
using Recordum.Data.Security;
using Recordum.Plugins.ChartEngine;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.ExportEngine;
using Recordum.Presentation.Bootstrap;

namespace Recordum.WPF.Bootstrap;

public sealed class ApplicationInitializer(
    RecordumOptions options,
    IDbKeyProvider keyProvider,
    IDbContextFactory<RecordumDbContext> dbFactory,
    IPluginManager pluginManager,
    IChartEngineActivator chartEngine,
    IExportEngineActivator exportEngine,
    IWebView2Availability webView2,
    BackupSchedulerHost backupHost,
    RetroactiveNoonBackfill retroBackfill,
    AttachmentFileImport attachmentImport)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Unlock the SQLCipher file ahead of EF migrations. PRAGMA key is applied
        // per-connection by the SqlCipherKeyConnectionInterceptor; this initial
        // open primes the file (and forces an error here if the key is wrong).
        using (var key = await keyProvider.GetKeyAsync(ct).ConfigureAwait(false))
        using (SqlCipherConnectionFactory.Open(options.DatabasePath, key))
        {
            // PRAGMA key applied; close so EF can manage its own connections.
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);

        // Manutenzione una tantum R9: le retroattive salvate a mezzanotte UTC vanno a mezzogiorno
        // locale. Dopo le migrazioni (ha bisogno dello schema aggiornato), prima di tutto cio' che
        // legge giorni. Nessun try/catch: se fallisce deve fallire l'avvio come una migrazione
        // fallita — un backfill saltato in silenzio e' peggio di un avvio che si lamenta.
        await retroBackfill.RunAsync(ct).ConfigureAwait(false);
        await attachmentImport.RunAsync(ct).ConfigureAwait(false);

        _ = pluginManager.ListEnabled();

        // First-party core plugin: enable + register the chart.renderer extension.
        await chartEngine.ActivateAsync(ct).ConfigureAwait(false);
        // Second first-party core plugin: register the 5 export.format formatters.
        await exportEngine.ActivateAsync(ct).ConfigureAwait(false);

        // Non-fatal probe — Win10 boxes without WebView2 Runtime fall back to TextBox.
        await webView2.ProbeAsync().ConfigureAwait(false);

        // Start the 60-minute auto-backup scheduler + orphan temp-dir cleanup.
        backupHost.Initialize();
    }
}
