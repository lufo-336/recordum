using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies PerTypePrefsV214: schema_version bumps to 2.14; the three new columns exist with the
/// right nullability/default; the repeatable CHECK rejects out-of-range values; and the data-aware
/// backfill flips repeatable=1 exactly for habits that already have a multi-series completion.
/// </summary>
public sealed class PerTypePrefsV214MigrationTests
{
    [Fact]
    public async Task Schema_version_is_2_21_after_full_migration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var setting = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.21", setting.Value);
    }

    [Fact]
    public async Task New_columns_round_trip_and_repeatable_defaults_false()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        db.HabitNodes.Add(new HabitNode
        {
            Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c",
            UiPrefsJson = "{\"strip\":\"Days14\"}",
        });
        db.HabitConfigs.Add(new HabitConfig
        {
            Id = Guid.NewGuid(), HabitNodeId = habitId,
            SeriesLabelsJson = "[\"Notte\",\"Riposino\"]",
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var node = await db.HabitNodes.SingleAsync(n => n.Id == habitId);
        var cfg = await db.HabitConfigs.SingleAsync(c => c.HabitNodeId == habitId);
        Assert.Equal("{\"strip\":\"Days14\"}", node.UiPrefsJson);
        Assert.Equal("[\"Notte\",\"Riposino\"]", cfg.SeriesLabelsJson);
        Assert.False(cfg.Repeatable); // NOT NULL DEFAULT 0
    }

    [Fact]
    public async Task Repeatable_check_constraint_rejects_out_of_range()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        await TestDbHelper.RawInsertHabitNodeAsync(connection, habitId, "c", "Counter");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO habit_configs (id, habit_node_id, schedule_type, streak_mode, streak_recovery, " +
            "has_integrated_timer, timer_auto_prompt, session_base_timer, quick_complete, ai_data_sharing, " +
            "created_at, updated_at, updated_at_ms, repeatable) " +
            "VALUES ($id, $h, 'Daily', 'Daily', 'None', 0, 0, 0, 0, 'None', " +
            "'2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', 1748736000000, 2);";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$h", habitId.ToString("D").ToUpperInvariant());
        await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Backfill_sets_repeatable_only_for_habits_with_multi_series_completions()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var multi = Guid.NewGuid();   // has a completion_value with series_index > 0  → repeatable
        var single = Guid.NewGuid();  // only series_index 0                            → stays 0

        // Stage rows against the schema just BEFORE this migration (columns not present yet).
        await using (var seed = TestDbHelper.CreateAtMigration(connection, "AttachmentV213"))
        {
            await TestDbHelper.RawInsertHabitNodeAsync(connection, multi, "Multi", "Counter");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, single, "Single", "Counter");
            await TestDbHelper.RawInsertHabitConfigAsync(connection, Guid.NewGuid(), multi);
            await TestDbHelper.RawInsertHabitConfigAsync(connection, Guid.NewGuid(), single);

            // SQL grezzo: alla 2.13 metriche, completions e valori non hanno ancora le colonne di sync (2.21),
            // che il modello EF di oggi invece scrive.
            var metricMulti = Guid.NewGuid();
            var metricSingle = Guid.NewGuid();
            var compMulti = Guid.NewGuid();
            var compSingle = Guid.NewGuid();
            await ExecAsync(connection, "INSERT INTO metric_definitions (id, habit_node_id, name, type, sort_order) VALUES ($id, $h, 'reps', 'Integer', 0)", ("$id", metricMulti), ("$h", multi));
            await ExecAsync(connection, "INSERT INTO metric_definitions (id, habit_node_id, name, type, sort_order) VALUES ($id, $h, 'reps', 'Integer', 0)", ("$id", metricSingle), ("$h", single));
            await ExecAsync(connection, "INSERT INTO completions (id, habit_node_id, completed_at, completed_at_ms, is_retroactive) VALUES ($id, $h, '2026-06-01 00:00:00', 1, 0)", ("$id", compMulti), ("$h", multi));
            await ExecAsync(connection, "INSERT INTO completions (id, habit_node_id, completed_at, completed_at_ms, is_retroactive) VALUES ($id, $h, '2026-06-01 00:00:00', 2, 0)", ("$id", compSingle), ("$h", single));
            await ExecAsync(connection, "INSERT INTO completion_values (id, completion_id, metric_definition_id, series_index, value_integer) VALUES ($id, $c, $m, 1, 10)", ("$id", Guid.NewGuid()), ("$c", compMulti), ("$m", metricMulti));
            await ExecAsync(connection, "INSERT INTO completion_values (id, completion_id, metric_definition_id, series_index, value_integer) VALUES ($id, $c, $m, 0, 5)", ("$id", Guid.NewGuid()), ("$c", compSingle), ("$m", metricSingle));
        }

        // Apply the remaining migration (PerTypePrefsV214 + its backfill).
        await using (var migrate = TestDbHelper.CreateFull(connection))
            await migrate.Database.MigrateAsync();

        await using var db = TestDbHelper.CreateFull(connection);
        var configs = await db.HabitConfigs.ToDictionaryAsync(c => c.HabitNodeId, c => c.Repeatable);
        Assert.True(configs[multi]);
        Assert.False(configs[single]);
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, params (string Name, Guid Value)[] args)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v.ToString("D").ToUpperInvariant());
        await cmd.ExecuteNonQueryAsync();
    }
}
