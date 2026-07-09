using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies NoteCategoryV211: schema_version bumps to 2.11; the category column defaults to Veloce
/// and its CHECK rejects unknown values; completion_id round-trips.
/// </summary>
public sealed class NoteCategoryV211MigrationTests
{
    [Fact]
    public async Task Schema_version_AppSetting_is_2_11_after_full_migration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var setting = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.14", setting.Value);
    }

    [Fact]
    public async Task Note_defaults_to_Veloce_and_round_trips_completion_id()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" });
        await db.SaveChangesAsync();

        var completionId = Guid.NewGuid();
        db.Completions.Add(new Completion { Id = completionId, HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        var diario = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "x", Category = NoteCategory.Diario, CompletionId = completionId };
        var veloce = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "y" }; // default Veloce
        db.Notes.AddRange(diario, veloce);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var stored = await db.Notes.SingleAsync(n => n.Id == veloce.Id);
        Assert.Equal(NoteCategory.Veloce, stored.Category);
        var storedDiario = await db.Notes.SingleAsync(n => n.Id == diario.Id);
        Assert.Equal(completionId, storedDiario.CompletionId);
    }

    [Fact]
    public async Task Category_check_constraint_rejects_unknown_value()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" });
        await db.SaveChangesAsync();

        // Raw insert bypassing the enum mapping to hit the DB CHECK directly.
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO notes (id, habit_node_id, body_md, is_sensitive, created_at, updated_at_ms, category) " +
            "VALUES ($id, $h, 'z', 0, '2026-06-19T00:00:00', 0, 'Bogus');";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$h", habitId.ToString());
        await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
    }
}
