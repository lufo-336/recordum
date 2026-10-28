using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Backup;
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Recordum.Data.Tests.Backup;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>
/// Databases made by the SQLCipher 4.5.2 bundle of before wave D9 (frozen in Fixtures/Cipher) still open with the same key.
/// A failure here, error 26, would tell a user with the right passphrase that it is wrong.
/// </summary>
public sealed class LegacyCipherDatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"recordum-legacy-cipher-{Guid.NewGuid():N}");

    public LegacyCipherDatabaseTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string CopyFixture(string file, params string[] companions)
    {
        var db = Path.Combine(_dir, file);
        File.Copy(CipherFixture.Path(file), db);
        foreach (var c in companions) File.Copy(CipherFixture.Path(file + c), db + c);
        return db;
    }

    private static T Scalar<T>(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T), CultureInfo.InvariantCulture);
    }

    private static RecordumDbContext Context(string db) => new(new DbContextOptionsBuilder<RecordumDbContext>()
        .UseSqlite($"Data Source={db}")
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new SqlCipherKeyConnectionInterceptor(new EncryptedDbFixture.FixedKeyProvider(CipherFixture.Key)))
        .Options);

    [Fact]
    public async Task A_database_made_by_the_sqlcipher_4_5_2_bundle_opens_with_the_same_content()
    {
        var db = CopyFixture(CipherFixture.Clean);
        using (var key = new KeyMaterial(CipherFixture.Key))
        using (var conn = SqlCipherConnectionFactory.Open(db, key))
            Scalar<string>(conn, "PRAGMA integrity_check").Should().Be("ok");
        SqliteConnection.ClearAllPools();

        await using var ctx = Context(db);
        (await DbSchemaVersion.ReadAsync(ctx, "", default)).Should().Be("2.19");
        await RecbakReferenceSeed.AssertKnownValuesAsync(ctx);
    }

    [Fact]
    public void A_wrong_key_on_the_legacy_database_fails_at_the_first_read_with_26()
    {
        var db = CopyFixture(CipherFixture.Clean);
        using var wrong = new KeyMaterial(new byte[KeyMaterial.LengthBytes]);
        using var conn = SqlCipherConnectionFactory.Open(db, wrong);   // opening does not fail: the first read does
        var act = () => Scalar<long>(conn, "SELECT COUNT(*) FROM sqlite_master");
        act.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(26);
    }

    [Fact]
    public void Without_its_journal_the_hot_journal_database_is_visibly_half_written()
    {
        // The .db alone: page 1 spilled with the schema entry of spill_filler, whose pages never made it. If this ever
        // reads clean, the fixture is not hot and the rollback test below would prove nothing.
        var db = CopyFixture(CipherFixture.HotJournal);
        using var key = new KeyMaterial(CipherFixture.Key);
        using var conn = SqlCipherConnectionFactory.Open(db, key);
        var act = () => Scalar<long>(conn, "SELECT COUNT(*) FROM habit_nodes");
        act.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(11);   // SQLITE_CORRUPT: malformed schema
    }

    [Fact]
    public async Task A_hot_journal_left_by_the_old_bundle_is_rolled_back()
    {
        var db = CopyFixture(CipherFixture.HotJournal, "-journal");
        new FileInfo(db + "-journal").Length.Should().BeGreaterThan(0);

        using (var key = new KeyMaterial(CipherFixture.Key))
        using (var conn = SqlCipherConnectionFactory.Open(db, key))
        {
            Scalar<string>(conn, "PRAGMA integrity_check").Should().Be("ok");
            Scalar<long>(conn, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'spill_filler'").Should().Be(0);
        }
        SqliteConnection.ClearAllPools();

        await using (var ctx = Context(db))
        {
            (await ctx.HabitNodes.SingleAsync(h => h.Id == RecbakReferenceSeed.RunId)).Name.Should().NotBe("HALF").And.Be("Morning run");
            await RecbakReferenceSeed.AssertKnownValuesAsync(ctx);
        }
        SqliteConnection.ClearAllPools();
        File.Exists(db + "-journal").Should().BeFalse("the rollback consumes the hot journal");
    }
}
