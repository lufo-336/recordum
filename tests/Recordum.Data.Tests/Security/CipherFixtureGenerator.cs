using FluentAssertions;
using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Data.Tests.Backup;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>The key and the path of the databases frozen by <see cref="CipherFixtureGenerator"/> (wave D9).</summary>
public static class CipherFixture
{
    /// <summary>32 bytes 0x00..0x1F. Not a secret: the fixture holds the reference seed, nothing real.</summary>
    private static readonly byte[] KeyBytes = Enumerable.Range(0, KeyMaterial.LengthBytes).Select(i => (byte)i).ToArray();

    /// <summary>A fresh copy every time: <see cref="KeyMaterial.Dispose"/> zeroes the bytes it was given.</summary>
    public static byte[] Key => (byte[])KeyBytes.Clone();

    public const string Clean = "sqlcipher-4.5.2-v2.19.db";
    public const string HotJournal = "sqlcipher-4.5.2-hot-journal.db";

    public static string Path(string file) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cipher", file);
}

/// <summary>
/// Writes the legacy-bundle databases into the folder named by RECORDUM_GENERATE_CIPHER_FIXTURE. Run ONCE, with the
/// SQLCipher 4.5.2 bundle (SQLitePCLRaw.bundle_e_sqlcipher 2.1.11), before the provider swap: after it, the output would
/// be a fixture of the new bundle and prove nothing. See Fixtures/Cipher/README.md.
/// </summary>
[Trait("Category", "FixtureGenerator")]
public class CipherFixtureGenerator
{
    [CipherGeneratorFact]
    public async Task Generate()
    {
        var folder = Path.GetFullPath(Environment.GetEnvironmentVariable("RECORDUM_GENERATE_CIPHER_FIXTURE")!);
        Directory.CreateDirectory(folder);

        var fixture = await EncryptedDbFixture.SeedAsync(new EncryptedDbFixture.FixedKeyProvider(CipherFixture.Key));
        try
        {
            await RecbakReferenceSeed.WriteAsync(fixture);
            SqliteConnection.ClearAllPools();

            var clean = Path.Combine(folder, CipherFixture.Clean);
            File.Copy(fixture.Options.DatabasePath, clean, overwrite: true);

            // Hot journal: copy db + journal while a transaction is open and its pages have spilled to the .db.
            var hot = Path.Combine(folder, CipherFixture.HotJournal);
            File.Copy(clean, hot, overwrite: true);
            File.Delete(hot + "-journal");
            using (var key = new KeyMaterial(CipherFixture.Key))
            using (var conn = SqlCipherConnectionFactory.Open(hot, key))
            {
                Exec(conn, "PRAGMA cache_size = 1;");
                Exec(conn, "BEGIN IMMEDIATE;");
                Exec(conn, $"DELETE FROM completions WHERE habit_node_id = '{RecbakReferenceSeed.RunId}';");
                Exec(conn, $"UPDATE habit_nodes SET name = 'HALF' WHERE id = '{RecbakReferenceSeed.RunId}';");
                // Force spill of the dirty pages to the .db so the pair really is hot.
                Exec(conn, "PRAGMA cache_spill = 1;");
                Exec(conn, "CREATE TABLE spill_filler (x BLOB);");
                for (var i = 0; i < 20; i++)
                    Exec(conn, "INSERT INTO spill_filler VALUES (randomblob(8000));");

                var inFlightDb = Path.Combine(folder, "inflight.db");
                CopyShared(hot, inFlightDb);
                CopyShared(hot + "-journal", inFlightDb + "-journal");
                Exec(conn, "ROLLBACK;");
                SqliteConnection.ClearAllPools();
                File.Move(inFlightDb, hot, overwrite: true);
                File.Move(inFlightDb + "-journal", hot + "-journal", overwrite: true);
            }
            SqliteConnection.ClearAllPools();
            File.Delete(hot + "-wal");

            new FileInfo(hot + "-journal").Length.Should().BeGreaterThan(0, "a hot journal is non-empty");
            File.ReadAllBytes(hot).Should().NotEqual(File.ReadAllBytes(clean), "pages must have spilled to the .db");
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void CopyShared(string from, string to)
    {
        using var src = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var dst = File.Create(to);
        src.CopyTo(dst);
    }
}

/// <summary>A skip (never a green pass) when RECORDUM_GENERATE_CIPHER_FIXTURE is not set.</summary>
public sealed class CipherGeneratorFactAttribute : FactAttribute
{
    public CipherGeneratorFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECORDUM_GENERATE_CIPHER_FIXTURE")))
            Skip = "Set RECORDUM_GENERATE_CIPHER_FIXTURE=<folder> to generate the legacy cipher fixtures.";
    }
}
