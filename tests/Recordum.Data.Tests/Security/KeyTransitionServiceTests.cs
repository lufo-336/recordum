using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

[SupportedOSPlatform("windows")]
public class KeyTransitionServiceTests
{
    [Fact]
    public async Task DPAPI_to_passphrase_then_back_round_trip()
    {
        var (dbPath, keyPath, saltPath) = Temp();
        try
        {
            var dpapi = new DpapiKeyProvider(keyPath);
            using (var key = await dpapi.GetKeyAsync())
            using (var conn = SqlCipherConnectionFactory.Open(dbPath, key))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (7);";
                await cmd.ExecuteNonQueryAsync();
            }
            SqliteConnection.ClearAllPools();

            var transition = new KeyTransitionService(
                new TestPaths(dbPath, keyPath, saltPath),
                dpapi,
                NullLogger<KeyTransitionService>.Instance);
            await transition.TransitionToPassphraseAsync("correct horse battery");

            Assert.False(File.Exists(keyPath));
            Assert.True(File.Exists(saltPath));

            var pp = new PassphraseKeyProvider("correct horse battery", saltPath);
            using (var key = await pp.GetKeyAsync())
            using (var conn = SqlCipherConnectionFactory.Open(dbPath, key))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT x FROM t;";
                Assert.Equal(7L, (long)(await cmd.ExecuteScalarAsync())!);
            }
            SqliteConnection.ClearAllPools();
        }
        finally { Cleanup(dbPath, keyPath, saltPath); }
    }

    [Fact]
    public async Task Rekey_failure_restores_backup_and_keeps_dpapi()
    {
        var (dbPath, keyPath, saltPath) = Temp();
        try
        {
            var dpapi = new DpapiKeyProvider(keyPath);
            using (var key = await dpapi.GetKeyAsync())
            using (var conn = SqlCipherConnectionFactory.Open(dbPath, key))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE t(x);";
                await cmd.ExecuteNonQueryAsync();
            }
            SqliteConnection.ClearAllPools();

            var transition = new KeyTransitionService(
                new TestPaths(dbPath, keyPath, saltPath),
                dpapi,
                NullLogger<KeyTransitionService>.Instance);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                transition.TransitionToPassphraseAsync("short").AsTask());

            Assert.True(File.Exists(keyPath));
            Assert.False(File.Exists(saltPath));

            using var key2 = await dpapi.GetKeyAsync();
            using var conn2 = SqlCipherConnectionFactory.Open(dbPath, key2);
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "SELECT name FROM sqlite_master;";
            Assert.NotNull(await cmd2.ExecuteScalarAsync());
            SqliteConnection.ClearAllPools();
        }
        finally { Cleanup(dbPath, keyPath, saltPath); }
    }

    private static (string db, string key, string salt) Temp()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"recordum-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return (Path.Combine(dir, "r.db"), Path.Combine(dir, "db.key"), Path.Combine(dir, "db.salt"));
    }

    private static void Cleanup(params string[] paths)
    {
        SqliteConnection.ClearAllPools();
        foreach (var p in paths)
        {
            try { if (File.Exists(p)) File.Delete(p); }
#pragma warning disable CA1031
            catch { /* best-effort */ }
#pragma warning restore CA1031
        }
        var dir = Path.GetDirectoryName(paths[0])!;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
#pragma warning disable CA1031
        catch { /* best-effort */ }
#pragma warning restore CA1031
    }

    private sealed record TestPaths(string DatabasePath, string KeyFilePath, string SaltFilePath) : IDbStoragePaths;
}
