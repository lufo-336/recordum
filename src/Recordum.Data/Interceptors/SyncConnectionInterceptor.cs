using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recordum.Core.Abstractions;
using Recordum.Data.Sync;

namespace Recordum.Data.Interceptors;

/// <summary>Registra a ogni apertura le funzioni che i trigger della sync chiamano (<see cref="SyncTriggerInstaller"/>):
/// <c>recordum_now_ms()</c> (l'ora da <see cref="IClock"/>, mai quella di SQLite) e <c>recordum_sync_applying()</c>.
/// Una connessione che scrive su una tabella che viaggia senza queste funzioni fallisce con "no such function".</summary>
public sealed class SyncConnectionInterceptor(IClock clock) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Register(connection, clock);

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Register(connection, clock);
        return Task.CompletedTask;
    }

    /// <summary>Per le connessioni che EF non apre da sé (una connessione già aperta passata a <c>UseSqlite</c>).
    /// Microsoft.Data.Sqlite tiene le funzioni sull'oggetto e le riapplica a ogni riapertura.</summary>
    public static void Register(DbConnection connection, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(clock);
        if (connection is not SqliteConnection sqlite) return;
        var state = SyncConnectionState.For(connection);
        sqlite.CreateFunction("recordum_now_ms", () => clock.UtcNowMs);
        sqlite.CreateFunction("recordum_sync_applying", () => state.Applying ? 1L : 0L);
    }
}
