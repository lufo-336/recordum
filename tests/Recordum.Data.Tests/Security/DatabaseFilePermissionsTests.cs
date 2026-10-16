using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>F1-SEC1: misurati con <c>File.GetUnixFileMode</c>, mai dedotti.</summary>
[Trait("Category", "UnixOnly")]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Collection(SqlCipherPoolCollection.Name)]
public sealed class DatabaseFilePermissionsTests : IDisposable
{
    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [DllImport("libc", EntryPoint = "umask")]
    private static extern uint SetUmask(uint mask);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-perm-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;

    public DatabaseFilePermissionsTests()
    {
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "recordum.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private sealed record Paths(string DatabasePath, string KeyFilePath, string SaltFilePath) : IDbStoragePaths;

    private DbUnlockService Service() => new(new Paths(_db, Path.Combine(_dir, "db.key"), Path.Combine(_dir, "db.salt")), platformKeyFactory: null, new NullSecretStore());

    private static KeyMaterial Key() => new(Enumerable.Range(0, KeyMaterial.LengthBytes).Select(i => (byte)i).ToArray());

    /// <summary>Una scrittura in WAL con la connessione ancora aperta: -wal e -shm esistono.</summary>
    private void WriteInWal(Action<SqliteConnection>? whileOpen = null, KeyMaterial? key = null)
    {
        using var conn = SqlCipherConnectionFactory.Open(_db, key ?? Key());
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS t(x); INSERT INTO t VALUES (1);";
        cmd.ExecuteNonQuery();
        whileOpen?.Invoke(conn);
    }

    [Fact]
    public void A_new_database_and_its_wal_and_shm_are_0600_under_umask_022()
    {
        var old = SetUmask(0x12); // 022
        try
        {
            // La creazione vera: il primo avvio, che scrive il salt e pre-crea il database 0600.
            var created = Service().UnlockAsync(DbUnlockState.NeedsCreation, "correct horse battery staple", rememberOnDevice: false).GetAwaiter().GetResult();
            Assert.Equal(UnlockOutcome.Unlocked, created.Outcome);
            using var provider = created.Provider as IDisposable;
            using var key = created.Provider!.GetKeyAsync().AsTask().GetAwaiter().GetResult();
            WriteInWal(key: key, whileOpen: _ =>
            {
                Assert.True(File.Exists(_db + "-wal") && File.Exists(_db + "-shm"));
                Assert.Equal(Private, File.GetUnixFileMode(_db));
                Assert.Equal(Private, File.GetUnixFileMode(_db + "-wal"));
                Assert.Equal(Private, File.GetUnixFileMode(_db + "-shm"));
            });
        }
        finally { _ = SetUmask(old); }
    }

    [Fact]
    public void Existing_0644_files_are_realigned_to_0600_and_still_readable()
    {
        var old = SetUmask(0x12);
        try
        {
            WriteInWal();
            SqliteConnection.ClearAllPools();
            // Una connessione tenuta aperta mantiene -wal e -shm (alla chiusura SQLite li toglie).
            using var keep = SqlCipherConnectionFactory.Open(_db, Key());
            using (var c = keep.CreateCommand()) { c.CommandText = "INSERT INTO t VALUES (2)"; c.ExecuteNonQuery(); }
            foreach (var p in new[] { _db, _db + "-wal", _db + "-shm" })
            {
                Assert.True(File.Exists(p), p);
                File.SetUnixFileMode(p, (UnixFileMode)0b110_100_100);   // 0644
            }

            Service().ReconcileOrphans();

            foreach (var p in new[] { _db, _db + "-wal", _db + "-shm" })
                Assert.Equal(Private, File.GetUnixFileMode(p));
            using var read = keep.CreateCommand();
            read.CommandText = "SELECT count(*) FROM t";
            Assert.Equal(2L, (long)read.ExecuteScalar()!);
        }
        finally { _ = SetUmask(old); }
    }
}
