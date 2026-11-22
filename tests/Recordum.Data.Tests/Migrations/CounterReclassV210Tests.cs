using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies the CounterReclassV210 data migration (spec multivalue §5):
///   1. A Resistance with a plain measured metric (no scale) is reclassified as Counter.
///   2. A pure-abstinence Resistance (no metrics) stays Resistance.
///   3. A Resistance whose metric is a local scale (labels_json set) stays Resistance.
///   4. schema_version is bumped to "2.10".
/// </summary>
public sealed class CounterReclassV210Tests
{
    [Fact]
    public async Task Reclass_converts_series_resistance_and_keeps_abstinence_and_scale()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        Guid flessioni, nientefumo, voglie;

        // Seed against the schema just BEFORE the reclass migration runs.
        await using (TestDbHelper.CreateAtMigration(connection, "NoteHistoryV29"))
        {
            flessioni = Guid.NewGuid(); nientefumo = Guid.NewGuid(); voglie = Guid.NewGuid();
            await TestDbHelper.RawInsertHabitNodeAsync(connection, flessioni, "Flessioni", "Resistance");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, nientefumo, "Niente fumo", "Resistance");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, voglie, "Voglie", "Resistance");

            // Raw SQL: labels_json no longer exists in the current EF model (ScaleAnchorsV220 drops it).
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO metric_definitions (id, habit_node_id, name, type, sort_order, labels_json) VALUES
                ($m1, $flessioni, 'Ripetizioni', 'Integer', 0, NULL),
                ($m2, $voglie, 'Intensità', 'Integer', 0, '["Bassa","Media","Alta"]');
                """;
            cmd.Parameters.AddWithValue("$m1", Guid.NewGuid().ToString("D").ToUpperInvariant());
            cmd.Parameters.AddWithValue("$m2", Guid.NewGuid().ToString("D").ToUpperInvariant());
            cmd.Parameters.AddWithValue("$flessioni", flessioni.ToString("D").ToUpperInvariant());
            cmd.Parameters.AddWithValue("$voglie", voglie.ToString("D").ToUpperInvariant());
            await cmd.ExecuteNonQueryAsync();
        }

        // Apply the remaining migrations (CounterReclassV210).
        await using (var migrate = TestDbHelper.CreateFull(connection))
            await migrate.Database.MigrateAsync();

        await using var db = TestDbHelper.CreateFull(connection);
        var nodes = await db.HabitNodes.IgnoreQueryFilters().ToListAsync();

        Assert.Equal(HabitType.Counter, nodes.Single(n => n.Id == flessioni).HabitType);
        Assert.Equal(HabitType.Resistance, nodes.Single(n => n.Id == nientefumo).HabitType);
        Assert.Equal(HabitType.Resistance, nodes.Single(n => n.Id == voglie).HabitType);

        var version = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.21", version.Value);
    }
}
