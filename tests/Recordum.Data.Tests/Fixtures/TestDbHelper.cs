using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Recordum.Data;

namespace Recordum.Data.Tests.Fixtures;

/// <summary>
/// Lightweight helpers for migration-level tests that need precise control over
/// which migrations have been applied to an in-memory SQLite connection.
/// </summary>
public static class TestDbHelper
{
    /// <summary>
    /// Creates a <see cref="RecordumDbContext"/> backed by the given connection
    /// with the full current model (all migrations applied when you call MigrateAsync).
    /// </summary>
    public static RecordumDbContext CreateFull(SqliteConnection connection)
    {
        var opts = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RecordumDbContext(opts);
    }

    /// <summary>
    /// Applies migrations only up to (and including) <paramref name="lastMigrationName"/>
    /// using <see cref="IMigrator.Migrate"/>. Returns the context — the caller is responsible
    /// for disposing it.
    /// </summary>
    /// <param name="connection">Open SQLite connection to use.</param>
    /// <param name="lastMigrationName">
    /// Simple name (without timestamp prefix) or the full migration ID of the last migration
    /// to apply. For example: <c>"CompletionCompletedAtMsIndex"</c>.
    /// </param>
    public static RecordumDbContext CreateAtMigration(SqliteConnection connection, string lastMigrationName)
    {
        var ctx = CreateFull(connection);
        var migrator = ctx.GetService<IMigrator>();
        migrator.Migrate(lastMigrationName);
        return ctx;
    }

    /// <summary>
    /// Inserts a legacy habit_node row with <c>is_pinned = 1</c> using raw SQL,
    /// bypassing EF so the row is written against the old schema (before PinScopeV24).
    /// Uses <c>node_type = 'Group'</c> to avoid the habit_type NOT NULL check constraint.
    /// </summary>
    public static async Task RawInsertLegacyPinnedHabitNodeAsync(SqliteConnection connection, string name)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO habit_nodes (id, name, color, node_type, ai_data_sharing,
                                     sort_order, is_archived, is_pinned, priority, is_locked,
                                     created_at, updated_at, updated_at_ms)
            VALUES ($id, $name, '#808080', 'Group', 'None',
                    0, 0, 1, 0, 0,
                    '2026-05-19T00:00:00Z', '2026-05-19T00:00:00Z', 1747612800000);
            """;

        // EF Core stores GUIDs as uppercase TEXT in SQLite.
        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "$id";
        idParam.Value = Guid.NewGuid().ToString("D").ToUpperInvariant();

        var nameParam = cmd.CreateParameter();
        nameParam.ParameterName = "$name";
        nameParam.Value = name;

        cmd.Parameters.Add(idParam);
        cmd.Parameters.Add(nameParam);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Raw-inserts a habit_nodes row using only columns stable since PinScopeV24 (no ui_prefs_json).
    /// For staging rows at a migration older than a later habit_node column addition, where EF's
    /// full-model INSERT would reference a not-yet-existing column.
    /// </summary>
    public static async Task RawInsertHabitNodeAsync(
        SqliteConnection connection, Guid id, string name, string habitType)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO habit_nodes (id, name, color, node_type, habit_type, ai_data_sharing,
                                     pin_scope, sort_order, is_archived, priority, is_locked,
                                     created_at, updated_at, updated_at_ms)
            VALUES ($id, $name, '#808080', 'Habit', $type, 'None',
                    'None', 0, 0, 0, 0,
                    '2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', 1748736000000);
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString("D").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$type", habitType);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Raw-inserts a habit_configs row with the NOT NULL columns stable since v2.4 (no repeatable /
    /// series_labels_json). Defaults: Daily schedule, Daily streak mode, None (no) recovery.
    /// </summary>
    public static async Task RawInsertHabitConfigAsync(
        SqliteConnection connection, Guid id, Guid habitNodeId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO habit_configs (id, habit_node_id, schedule_type, streak_mode, streak_recovery,
                                       has_integrated_timer, timer_auto_prompt, session_base_timer,
                                       quick_complete, ai_data_sharing,
                                       created_at, updated_at, updated_at_ms)
            VALUES ($id, $h, 'Daily', 'Daily', 'None',
                    0, 0, 0,
                    0, 'None',
                    '2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', 1748736000000);
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString("D").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$h", habitNodeId.ToString("D").ToUpperInvariant());
        await cmd.ExecuteNonQueryAsync();
    }
}
