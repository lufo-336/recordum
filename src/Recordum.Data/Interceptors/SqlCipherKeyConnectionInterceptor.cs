using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Interceptors;

public sealed class SqlCipherKeyConnectionInterceptor(IDbKeyProvider keyProvider) : DbConnectionInterceptor
{
    /// <summary>How long a write waits for a transient lock from another connection before giving up (ms).</summary>
    internal const int BusyTimeoutMs = 5000;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var key = await keyProvider.GetKeyAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        // PRAGMA key MUST be the first statement; busy_timeout makes a writer wait for a transient
        // lock from another connection instead of failing instantly with SQLITE_BUSY. See B-dbLock.
        cmd.CommandText = $"PRAGMA key = \"x'{Convert.ToHexString(key.RawKey)}'\"; PRAGMA busy_timeout = {BusyTimeoutMs};";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var key = keyProvider.GetKeyAsync().AsTask().GetAwaiter().GetResult();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA key = \"x'{Convert.ToHexString(key.RawKey)}'\"; PRAGMA busy_timeout = {BusyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }
}
