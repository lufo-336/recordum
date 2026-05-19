using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

public static class SqlCipherConnectionFactory
{
    public static SqliteConnection Open(string path, KeyMaterial key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(key);
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA key = \"x'{Convert.ToHexString(key.RawKey)}'\";";
        cmd.ExecuteNonQuery();
        return conn;
    }
}
