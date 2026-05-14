using Microsoft.EntityFrameworkCore;
using Recordum.Data;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.WPF.Bootstrap;

public sealed class ApplicationInitializer(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IPluginManager pluginManager)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);

        // Plugin host is already constructed by DI. No plugins to enable in Phase 3
        // (consumers are first-party services that talk to the contracts directly).
        // Touching the manager here keeps the wiring honest: a future call site can
        // enumerate it, and a failure to resolve would surface at startup, not later.
        _ = pluginManager.ListEnabled();
    }
}
