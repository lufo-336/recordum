using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Security;
using Recordum.Data;
using Recordum.Data.Security;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.WPF.Bootstrap;

public sealed class ApplicationInitializer(
    RecordumOptions options,
    IDbKeyProvider keyProvider,
    IDbContextFactory<RecordumDbContext> dbFactory,
    IPluginManager pluginManager)
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

        _ = pluginManager.ListEnabled();
    }
}
