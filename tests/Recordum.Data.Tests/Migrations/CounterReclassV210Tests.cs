using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
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
        await using (var seed = TestDbHelper.CreateAtMigration(connection, "NoteHistoryV29"))
        {
            flessioni = Guid.NewGuid(); nientefumo = Guid.NewGuid(); voglie = Guid.NewGuid();
            await TestDbHelper.RawInsertHabitNodeAsync(connection, flessioni, "Flessioni", "Resistance");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, nientefumo, "Niente fumo", "Resistance");
            await TestDbHelper.RawInsertHabitNodeAsync(connection, voglie, "Voglie", "Resistance");

            seed.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(), HabitNodeId = flessioni, Name = "Ripetizioni",
                Type = MetricType.Integer, SortOrder = 0,
            });
            seed.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(), HabitNodeId = voglie, Name = "Intensità",
                Type = MetricType.Integer, SortOrder = 0,
                LabelsJson = "[\"Bassa\",\"Media\",\"Alta\"]",
            });
            await seed.SaveChangesAsync();
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
        Assert.Equal("2.17", version.Value);
    }
}
