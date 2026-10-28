using FluentAssertions;
using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>
/// A new database is born in SQLCipher 4 format, not in the ChaCha20 default of SQLite3 Multiple Ciphers: otherwise the
/// next provider change (or a step back) would not read it (wave D9, review focus 5).
/// </summary>
public sealed class CipherFormatTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"recordum-cipher-format-{Guid.NewGuid():N}");

    public CipherFormatTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string ReadRow(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT x FROM t";
        return (string)cmd.ExecuteScalar()!;
    }

    private static SqliteConnection OpenUnkeyed(string db)
    {
        var conn = new SqliteConnection($"Data Source={db};Pooling=False");
        conn.Open();
        return conn;
    }

    [Fact]
    public void A_new_database_is_written_in_sqlcipher_4_format()
    {
        var db = Path.Combine(_dir, "new.db");
        using (var key = new KeyMaterial(CipherFixture.Key))
        using (var conn = SqlCipherConnectionFactory.Open(db, key))
            Exec(conn, "CREATE TABLE t (x TEXT); INSERT INTO t VALUES ('kept');");
        SqliteConnection.ClearAllPools();

        // The same key under the provider's default cipher: a ChaCha20 file would open here.
        using (var conn = OpenUnkeyed(db))
        {
            Exec(conn, $"PRAGMA key = \"x'{Convert.ToHexString(CipherFixture.Key)}'\";");
            var act = () => ReadRow(conn);
            act.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(26);
        }

        using (var key = new KeyMaterial(CipherFixture.Key))
        using (var conn = OpenUnkeyed(db))
        {
            Exec(conn, CipherPragmas.Key(key));
            ReadRow(conn).Should().Be("kept");
        }
    }
}
