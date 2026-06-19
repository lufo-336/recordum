using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies correctness of the ActiveTimerV25 migration:
///   1. schema_version is bumped to 2.5 after all migrations run.
///   2. active_timer table exists and enforces the UNIQUE constraint on habit_node_id.
/// </summary>
public sealed class ActiveTimerV25MigrationTests
{
    /// <summary>
    /// After all migrations run, the app_settings row for schema_version must be "2.11"
    /// (latest: NoteCategoryV211 — previous milestones bumped to 2.5–2.10 intermediately).
    /// </summary>
    [Fact]
    public async Task Schema_version_AppSetting_is_2_11_after_full_migration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var setting = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.11", setting.Value);
    }

    /// <summary>
    /// After migration, inserting two ActiveTimer rows with the same HabitNodeId
    /// must throw a DbUpdateException due to the UNIQUE index ux_active_timer_habit_node_id.
    /// </summary>
    [Fact]
    public async Task Active_timer_table_enforces_unique_habit_node_id()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();

        db.HabitNodes.Add(new HabitNode
        {
            Id = habitId,
            NodeType = NodeType.Habit,
            HabitType = HabitType.Counter,
            Name = "x",
        });
        await db.SaveChangesAsync();

        db.ActiveTimers.Add(new ActiveTimer
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            StartedAtMs = 1,
            TimerMode = TimerMode.Stopwatch,
        });
        await db.SaveChangesAsync();

        db.ActiveTimers.Add(new ActiveTimer
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            StartedAtMs = 2,
            TimerMode = TimerMode.Stopwatch,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
