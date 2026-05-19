using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

public class SqlCipherConnectionFactoryTests
{
    [Fact]
    public async Task Open_encrypted_db_with_correct_key_succeeds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-test-{Guid.NewGuid():N}.db");
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            using (var conn = SqlCipherConnectionFactory.Open(path, new KeyMaterial(key)))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (42);";
                await cmd.ExecuteNonQueryAsync();
            }
            var key2 = new byte[32];
            Array.Copy(key, key2, 32);
            using (var conn = SqlCipherConnectionFactory.Open(path, new KeyMaterial(key2)))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT x FROM t;";
                Assert.Equal(42L, (long)(await cmd.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Open_with_wrong_key_throws_on_first_read()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-test-{Guid.NewGuid():N}.db");
        try
        {
            var good = new byte[32]; RandomNumberGenerator.Fill(good);
            var bad = new byte[32]; RandomNumberGenerator.Fill(bad);
            using (var conn = SqlCipherConnectionFactory.Open(path, new KeyMaterial(good)))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE t(x);";
                await cmd.ExecuteNonQueryAsync();
            }
            SqliteConnection.ClearAllPools();
            using var conn2 = SqlCipherConnectionFactory.Open(path, new KeyMaterial(bad));
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "SELECT * FROM t;";
            await Assert.ThrowsAsync<SqliteException>(() => cmd2.ExecuteReaderAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
