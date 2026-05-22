using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies correctness of the PinScopeV24 migration:
///   1. Legacy is_pinned = 1 rows are migrated to PinScope.Local.
///   2. Fresh rows default to PinScope.None.
///   3. schema_version is bumped to 2.4 after all migrations run.
/// </summary>
public sealed class PinScopeMigrationTests
{
    /// <summary>
    /// A row inserted against the pre-PinScopeV24 schema with is_pinned = 1
    /// must appear as PinScope.Local after the migration runs.
    /// </summary>
    [Fact]
    public async Task Existing_is_pinned_rows_become_PinScope_Local()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // Apply all migrations up to (but not including) PinScopeV24.
        await using (var seedDb = TestDbHelper.CreateAtMigration(
            connection, "CompletionCompletedAtMsIndex"))
        {
            // Insert a legacy row while is_pinned column still exists.
            await TestDbHelper.RawInsertLegacyPinnedHabitNodeAsync(connection, name: "Pinned habit");
        }

        // Now apply the remaining migrations (PinScopeV24).
        await using var targetDb = TestDbHelper.CreateFull(connection);
        await targetDb.Database.MigrateAsync();

        // The PinScopeV24 migration must have converted is_pinned=1 → PinScope.Local.
        var node = await targetDb.HabitNodes.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(PinScope.Local, node.PinScope);
        Assert.Equal("Pinned habit", node.Name);
    }

    /// <summary>
    /// A HabitNode inserted into a fresh fully-migrated database must default to PinScope.None.
    /// </summary>
    [Fact]
    public async Task Fresh_DB_defaults_PinScope_to_None()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        db.HabitNodes.Add(new Recordum.Core.Models.HabitNode
        {
            Id = Guid.NewGuid(),
            Name = "Fresh",
            NodeType = NodeType.Group,
            Color = "#808080",
        });
        await db.SaveChangesAsync();

        var node = await db.HabitNodes.SingleAsync();
        Assert.Equal(PinScope.None, node.PinScope);
    }

    /// <summary>
    /// After all migrations run up to and including PinScopeV24 (but not ActiveTimerV25),
    /// the app_settings row for schema_version must be "2.4".
    /// The PinScopeV24 migration issues an UPDATE to bump the value from "2.3" to "2.4".
    /// </summary>
    [Fact]
    public async Task Schema_version_AppSetting_is_2_4_after_PinScopeV24_migration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = TestDbHelper.CreateAtMigration(connection, "PinScopeV24");

        var setting = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.4", setting.Value);
    }
}
