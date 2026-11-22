using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

public sealed class SyncFoundationV221MigrationTests
{
    private const long ExpectedJan2Ms = 1767323045000L; // 2026-01-02T03:04:05Z

    [Fact]
    public async Task UpdatedAtMsBackfilledFromUpdatedAtOrCreatedAt()
    {
        await using var conn = await OpenAt220Async();

        // app_settings ha updated_at (nessun created_at).
        await ExecAsync(conn, """
            INSERT INTO app_settings (key, value, updated_at)
            VALUES ('sync_backfill_probe', 'x', '2026-01-02T03:04:05Z');
            """);

        // tags ha solo created_at, nel formato che EF scrive (spazio, frazione a 7 cifre): 123 ms, non troncati.
        var tagId = Guid.NewGuid();
        await ExecAsync(conn, """
            INSERT INTO tags (id, name, created_at)
            VALUES ($id, 'sync-backfill-tag', '2026-01-02 03:04:05.1234567');
            """, ("$id", G(tagId)));

        // completions non ha né updated_at né created_at: il backfill usa il suo istante, completed_at_ms.
        var habitId = Guid.NewGuid();
        var completionId = Guid.NewGuid();
        await TestDbHelper.RawInsertHabitNodeAsync(conn, habitId, "Corsa", "Action");
        await ExecAsync(conn, """
            INSERT INTO completions (id, habit_node_id, completed_at, completed_at_ms, is_retroactive)
            VALUES ($c, $h, '2026-01-02T03:04:05Z', 1767323045000, 0);
            """, ("$c", G(completionId)), ("$h", G(habitId)));

        await MigrateAsync(conn);

        (await ScalarAsync(conn, "SELECT updated_at_ms FROM app_settings WHERE key = 'sync_backfill_probe'"))
            .Should().Be(ExpectedJan2Ms);
        (await ScalarAsync(conn, $"SELECT updated_at_ms FROM tags WHERE id = '{G(tagId)}'"))
            .Should().Be(ExpectedJan2Ms + 123);
        (await ScalarAsync(conn, $"SELECT updated_at_ms FROM completions WHERE id = '{G(completionId)}'"))
            .Should().Be(ExpectedJan2Ms);
        // Una tabella figlia senza colonne di tempo resta a 0.
        (await ScalarAsync(conn, "SELECT count(*) FROM chart_series WHERE updated_at_ms <> 0")).Should().Be(0L);
    }

    [Fact]
    public async Task TombstoneAndBaseTablesExistWithPrimaryKeys()
    {
        await using var conn = await OpenAt220Async();
        await MigrateAsync(conn);

        var tombstoneCols = await RowsAsync(conn, "PRAGMA table_info('sync_tombstone')");
        tombstoneCols.Select(r => (string)r[1]!).Should().BeEquivalentTo(
            ["entity", "entity_id", "deleted_at_ms", "device_id", "alias_of"]);
        tombstoneCols
            .Where(r => (long)r[5]! > 0)
            .OrderBy(r => (long)r[5]!)
            .Select(r => (string)r[1]!)
            .Should().Equal("entity", "entity_id");

        var tombstonePkIndexes = await RowsAsync(conn, "PRAGMA index_list('sync_tombstone')");
        tombstonePkIndexes.Should().ContainSingle(r => (long)r[2]! == 1L && (string)r[3]! == "pk");

        var baseCols = await RowsAsync(conn, "PRAGMA table_info('sync_base')");
        baseCols.Select(r => (string)r[1]!).Should().BeEquivalentTo(
            ["peer_device_id", "entity", "entity_id", "row_json"]);
        baseCols
            .Where(r => (long)r[5]! > 0)
            .OrderBy(r => (long)r[5]!)
            .Select(r => (string)r[1]!)
            .Should().Equal("peer_device_id", "entity", "entity_id");

        var basePkIndexes = await RowsAsync(conn, "PRAGMA index_list('sync_base')");
        basePkIndexes.Should().ContainSingle(r => (long)r[2]! == 1L && (string)r[3]! == "pk");
    }

    [Fact]
    public async Task SchemaVersionIs221()
    {
        await using var conn = await OpenAt220Async();
        await MigrateAsync(conn);

        (await ScalarAsync(conn, "SELECT value FROM app_settings WHERE key = 'schema_version'"))
            .Should().Be("2.21");
    }

    private static string G(Guid g) => g.ToString("D").ToUpperInvariant();

    private static async Task<SqliteConnection> OpenAt220Async()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        await using (TestDbHelper.CreateAtMigration(conn, "ScaleAnchorsV220")) { }
        return conn;
    }

    private static async Task MigrateAsync(SqliteConnection conn)
    {
        await using var db = TestDbHelper.CreateFull(conn);
        await db.Database.MigrateAsync();
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, params (string Name, object Value)[] args)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var r = await cmd.ExecuteScalarAsync();
        return r is DBNull ? null : r;
    }

    private static async Task<List<object?[]>> RowsAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
