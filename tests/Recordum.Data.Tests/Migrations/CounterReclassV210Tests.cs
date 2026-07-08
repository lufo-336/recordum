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
    private static HabitNode Resistance(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Color = "#808080",
        NodeType = NodeType.Habit,
        HabitType = HabitType.Resistance,
        CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtMs = 1_748_736_000_000,
    };

    [Fact]
    public async Task Reclass_converts_series_resistance_and_keeps_abstinence_and_scale()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        Guid flessioni, nientefumo, voglie;

        // Seed against the schema just BEFORE the reclass migration runs.
        await using (var seed = TestDbHelper.CreateAtMigration(connection, "NoteHistoryV29"))
        {
            var h1 = Resistance("Flessioni");           // measured series → becomes Counter
            var h2 = Resistance("Niente fumo");          // pure abstinence → stays Resistance
            var h3 = Resistance("Voglie");               // local scale     → stays Resistance
            flessioni = h1.Id; nientefumo = h2.Id; voglie = h3.Id;
            seed.HabitNodes.AddRange(h1, h2, h3);

            seed.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(), HabitNodeId = h1.Id, Name = "Ripetizioni",
                Type = MetricType.Integer, SortOrder = 0,
            });
            seed.MetricDefinitions.Add(new MetricDefinition
            {
                Id = Guid.NewGuid(), HabitNodeId = h3.Id, Name = "Intensità",
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
        Assert.Equal("2.13", version.Value);
    }
}
