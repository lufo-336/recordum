using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Models;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Recordum.Data.Tests.Fixtures.Sync;

namespace Recordum.Data.Tests.Sync;

public sealed class SyncRowCodecTests
{
    private static SyncRow Row(params (string Name, string Json)[] fields) =>
        new("x", fields.ToDictionary(f => f.Name, f => JsonDocument.Parse(f.Json).RootElement.Clone(), StringComparer.Ordinal));

    [Fact]
    public void CanonicalIsSortedNameZeroValueZero()
    {
        var row = Row(("id", "\"ignored\""), ("b", "2"), ("a", "\"x\""), ("c", "true"), ("d", "null"), ("e", "1.5"), ("f", "false"));

        // a\0x\0 b\0 2\0 c\0 true\0 d\0 null\0 e\0 1.5\0 f\0 false\0
        var expected = "a\0x\0b\02\0c\0true\0d\0null\0e\01.5\0f\0false\0"u8.ToArray();
        SyncRowCodec.Canonical(row).Should().Equal(expected);
    }

    [Theory]
    [InlineData("1E+15", "1000000000000000")] // sotto la soglia: notazione fissa
    [InlineData("1E+16", "10000000000000000")]
    [InlineData("1E+17", "1E+17")] // dalla x = 17 scientifica
    [InlineData("1.5E-05", "1.5E-05")] // x = -5: scientifica
    [InlineData("0.0001", "0.0001")] // x = -4: fissa
    [InlineData("0.00001", "1E-05")]
    [InlineData("12345678901234567000", "1.2345678901234567E+19")]
    [InlineData("18446744073709551616", "1.8446744073709552E+19")] // intero oltre Int64
    [InlineData("-9223372036854775808", "-9223372036854775808")] // intero che entra in Int64: cifre
    [InlineData("1.5", "1.5")]
    public void CanonicalNumberFormIsTheDotNetRoundTripForm(string json, string expectedText)
    {
        var expected = Encoding.UTF8.GetBytes("n\0" + expectedText + "\0");
        SyncRowCodec.Canonical(Row(("n", json))).Should().Equal(expected);
    }

    [Fact]
    public void CanonicalOrdersFieldNamesByUtf8Bytes()
    {
        // "Z" (0x5A) < "_" (0x5F) < "a" (0x61) < "é" (0xC3 0xA9)
        var row = Row(("é", "\"1\""), ("a", "\"2\""), ("_", "\"3\""), ("Z", "\"4\""));
        var expected = new List<byte>();
        foreach (var (n, v) in new[] { ("Z", "4"), ("_", "3"), ("a", "2"), ("é", "1") })
        {
            expected.AddRange(Encoding.UTF8.GetBytes(n));
            expected.Add(0);
            expected.AddRange(Encoding.UTF8.GetBytes(v));
            expected.Add(0);
        }

        SyncRowCodec.Canonical(row).Should().Equal(expected);
    }

    [Theory]
    [InlineData("tag", "id", "\"abc\"")]
    [InlineData("tag", "id", "\"abcdef01-0000-0000-0000-000000000002\"")] // GUID minuscolo: non e' la forma salvata
    [InlineData("tag", "created_at", "\"2026-02-10T14:30:00Z\"")]
    [InlineData("tag", "created_at", "\"yesterday\"")]
    [InlineData("tag", "updated_at_ms", "1.5")]
    [InlineData("tag", "updated_at_ms", "\"7\"")]
    [InlineData("goal", "target_value", "\"abc\"")]
    [InlineData("goal", "target_value", "\"1,5\"")]
    [InlineData("streak_pause", "pause_from", "\"2026-13-01\"")]
    [InlineData("habit_node", "reset_time", "\"25:00:00\"")]
    [InlineData("habit_node", "sort_order", "3000000000")]
    [InlineData("habit_node", "node_type", "\"Bogus\"")]
    [InlineData("habit_node", "is_archived", "1")]
    public async Task ToDbRejectsWrongContentPerType(string entityName, string field, string json)
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var codec = new SyncRowCodec(db.Model);
        var entity = SyncCatalog.Get(entityName);

        var act = () => codec.ToDb(entity, Row((field, json)));
        act.Should().Throw<SyncFormatException>();
    }

    [Fact]
    public async Task ToDbKeepsAbsentFieldsAbsentAndNullAsNull()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var codec = new SyncRowCodec(db.Model);

        var dbRow = codec.ToDb(SyncCatalog.Get("tag"), Row(("name", "\"n\""), ("color", "null")));
        dbRow.Should().ContainKey("color").WhoseValue.Should().BeNull();
        dbRow.Should().NotContainKey("created_at");
        dbRow["name"].Should().Be("n");
    }

    [Fact]
    public async Task ToDbAcceptsTheStoredForms()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var codec = new SyncRowCodec(db.Model);

        codec.ToDb(SyncCatalog.Get("tag"), Row(("id", "\"ABCDEF01-0000-0000-0000-000000000002\""), ("created_at", "\"2026-02-10 14:30:00.1234567\"")))
            .Should().NotBeEmpty();
        codec.ToDb(SyncCatalog.Get("habit_node"), Row(("reset_time", "\"04:30:00\""), ("sort_order", "2147483647")))
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task StoredFormsAreTheOnesTheDocSays()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.Completions.Add(new Completion
        {
            Id = new Guid("ABCDEF01-0000-0000-0000-0000000000AB"),
            HabitNodeId = SyncSeed.HabitNodeId,
            CompletedAt = new DateTime(2026, 2, 10, 14, 30, 0, DateTimeKind.Utc).AddTicks(1234567),
            CompletedAtMs = 1,
            UpdatedAtMs = 1,
        });
        await db.SaveChangesAsync();

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        Scalar(conn, "SELECT id FROM tags WHERE name = 'deep-work'").Should().Be("ABCDEF01-0000-0000-0000-000000000002");
        Scalar(conn, "SELECT id FROM completions WHERE completed_at_ms = 1").Should().Be("ABCDEF01-0000-0000-0000-0000000000AB");
        Scalar(conn, "SELECT completed_at FROM completions WHERE completed_at_ms = 1").Should().Be("2026-02-10 14:30:00.1234567");
        Scalar(conn, "SELECT pause_from FROM streak_pauses LIMIT 1").Should().Be("2026-02-03");
        Scalar(conn, "SELECT reset_time FROM habit_nodes WHERE reset_time IS NOT NULL").Should().Be("04:30:00");
    }

    private static string Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (string)cmd.ExecuteScalar()!;
    }
}
