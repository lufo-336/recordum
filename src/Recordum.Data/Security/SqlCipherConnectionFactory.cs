using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

public static class SqlCipherConnectionFactory
{
    /// <summary>How long a write waits for a transient lock from another connection before giving up (ms).</summary>
    internal const int BusyTimeoutMs = 5000;

    public static SqliteConnection Open(string path, KeyMaterial key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(key);
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        // PRAGMA key MUST be the first statement; busy_timeout makes a writer wait for a transient
        // lock from another connection instead of failing instantly with SQLITE_BUSY. See B-dbLock.
        cmd.CommandText = $"PRAGMA key = \"x'{Convert.ToHexString(key.RawKey)}'\"; PRAGMA busy_timeout = {BusyTimeoutMs};";
        cmd.ExecuteNonQuery();
        return conn;
    }
}
