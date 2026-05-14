using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;
using Xunit;

namespace Recordum.Data.Tests.Schema;

/// <summary>
/// Verifies cascade rules per CLAUDE.md §6.3.
/// Each test inserts parent + child, deletes parent, then asserts the correct behaviour:
///   CASCADE   → child is gone
///   RESTRICT  → delete throws SqliteException (FK constraint violation)
///   SET NULL  → child remains, FK column is null
///
/// A separate DbContext + temp file per test to keep tests fully isolated.
/// </summary>
public sealed class CascadeRulesTests : IDisposable
{
    // One shared migrated DB per test class — each test gets its own RecordumDbContext instance
    // pointing at the same file, but tests modify data independently using unique IDs.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"recordum-cascade-{Guid.NewGuid():N}.db");

    public CascadeRulesTests()
    {
        using var ctx = BuildContext();
        ctx.Database.Migrate();
        // Enable FK enforcement (SQLite requires explicit opt-in per connection)
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
    }

    private RecordumDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RecordumDbContext(options);
    }

    // Helper to enable FK enforcement on every connection.
    // EF Core SQLite does NOT automatically enable PRAGMA foreign_keys.
    private static void EnableForeignKeys(RecordumDbContext ctx) =>
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");

    private static HabitNode MakeHabit() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test habit",
        NodeType = NodeType.Habit,
        HabitType = HabitType.Action,
        Color = "#808080",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // CASCADE rules
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HabitNode_to_HabitNodeTag_is_CASCADE()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var tag = new Tag { Id = Guid.NewGuid(), Name = $"tag-{Guid.NewGuid():N}" };
        var node = MakeHabit();
        ctx.Tags.Add(tag);
        ctx.HabitNodes.Add(node);
        ctx.SaveChanges();

        ctx.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = node.Id, TagId = tag.Id });
        ctx.SaveChanges();

        // Soft-delete and then hard-delete the node (bypass query filter)
        var toDelete = ctx.HabitNodes.IgnoreQueryFilters().Single(n => n.Id == node.Id);
        ctx.HabitNodes.Remove(toDelete);
        ctx.SaveChanges();

        // The join row should be gone (CASCADE)
        Assert.False(ctx.HabitNodeTags.Any(nt => nt.HabitNodeId == node.Id));
    }

    [Fact]
    public void Tag_to_HabitNodeTag_is_CASCADE()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var tag = new Tag { Id = Guid.NewGuid(), Name = $"tag-{Guid.NewGuid():N}" };
        var node = MakeHabit();
        ctx.Tags.Add(tag);
        ctx.HabitNodes.Add(node);
        ctx.SaveChanges();

        ctx.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = node.Id, TagId = tag.Id });
        ctx.SaveChanges();

        ctx.Tags.Remove(tag);
        ctx.SaveChanges();

        // The join row should be gone (CASCADE)
        Assert.False(ctx.HabitNodeTags.Any(nt => nt.TagId == tag.Id));
    }

    [Fact]
    public void Completion_to_CompletionValue_is_CASCADE()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var node = MakeHabit();
        ctx.HabitNodes.Add(node);
        ctx.SaveChanges();

        var metric = new MetricDefinition
        {
            Id = Guid.NewGuid(),
            HabitNodeId = node.Id,
            Name = "reps",
            Type = MetricType.Integer,
        };
        ctx.MetricDefinitions.Add(metric);
        ctx.SaveChanges();

        var completion = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = node.Id,
            CompletedAt = DateTime.UtcNow,
            CompletedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        ctx.Completions.Add(completion);
        ctx.SaveChanges();

        var value = new CompletionValue
        {
            Id = Guid.NewGuid(),
            CompletionId = completion.Id,
            MetricDefinitionId = metric.Id,
            SeriesIndex = 0,
            ValueInteger = 10,
        };
        ctx.CompletionValues.Add(value);
        ctx.SaveChanges();

        ctx.Completions.Remove(completion);
        ctx.SaveChanges();

        // CompletionValue should be gone (CASCADE)
        Assert.False(ctx.CompletionValues.Any(v => v.Id == value.Id));
    }

    [Fact]
    public void Completion_to_CompletionSegment_is_CASCADE()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var node = MakeHabit();
        ctx.HabitNodes.Add(node);
        ctx.SaveChanges();

        var segDef = new SegmentDefinition
        {
            Id = Guid.NewGuid(),
            HabitNodeId = node.Id,
            Name = "Work",
            SortOrder = 0,
        };
        ctx.SegmentDefinitions.Add(segDef);
        ctx.SaveChanges();

        var completion = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = node.Id,
            CompletedAt = DateTime.UtcNow,
            CompletedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        ctx.Completions.Add(completion);
        ctx.SaveChanges();

        var segment = new CompletionSegment
        {
            Id = Guid.NewGuid(),
            CompletionId = completion.Id,
            SegmentDefinitionId = segDef.Id,
            SegmentIndex = 0,
            StartedAt = DateTime.UtcNow,
            StartedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        ctx.CompletionSegments.Add(segment);
        ctx.SaveChanges();

        ctx.Completions.Remove(completion);
        ctx.SaveChanges();

        // CompletionSegment should be gone (CASCADE)
        Assert.False(ctx.CompletionSegments.Any(s => s.Id == segment.Id));
    }

    [Fact]
    public void Chart_to_ChartSeries_is_CASCADE()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var node = MakeHabit();
        ctx.HabitNodes.Add(node);
        ctx.SaveChanges();

        var chart = new Chart
        {
            Id = Guid.NewGuid(),
            Name = "Test chart",
            ChartType = "line",
        };
        ctx.Charts.Add(chart);
        ctx.SaveChanges();

        var series = new ChartSeries
        {
            Id = Guid.NewGuid(),
            ChartId = chart.Id,
            HabitNodeId = node.Id,
            SortOrder = 0,
        };
        ctx.ChartSeries.Add(series);
        ctx.SaveChanges();

        ctx.Charts.Remove(chart);
        ctx.SaveChanges();

        // ChartSeries should be gone (CASCADE)
        Assert.False(ctx.ChartSeries.Any(s => s.Id == series.Id));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SET NULL rules
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HabitNode_to_AIInsight_is_SET_NULL()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var node = MakeHabit();
        ctx.HabitNodes.Add(node);
        ctx.SaveChanges();

        var insight = new AIInsight
        {
            Id = Guid.NewGuid(),
            HabitNodeId = node.Id,
            InsightType = "test",
            BodyMd = "body",
            GeneratedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(7),
        };
        ctx.AIInsights.Add(insight);
        ctx.SaveChanges();

        // Soft-delete the node record directly (bypass soft delete filter) and hard-delete
        var toDelete = ctx.HabitNodes.IgnoreQueryFilters().Single(n => n.Id == node.Id);
        ctx.HabitNodes.Remove(toDelete);
        ctx.SaveChanges();

        // AIInsight should still exist but HabitNodeId should be NULL (SET NULL)
        var reloadedInsight = ctx.AIInsights.Single(i => i.Id == insight.Id);
        Assert.Null(reloadedInsight.HabitNodeId);
    }

    [Fact]
    public void ExportTemplate_to_ExportJob_is_SET_NULL()
    {
        using var ctx = BuildContext();
        EnableForeignKeys(ctx);

        var template = new ExportTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Test template",
            LayoutJson = "{}",
        };
        ctx.ExportTemplates.Add(template);
        ctx.SaveChanges();

        var job = new ExportJob
        {
            Id = Guid.NewGuid(),
            ExportTemplateId = template.Id,
            Format = ExportFormat.Pdf,
            GeneratedAt = DateTime.UtcNow,
        };
        ctx.ExportJobs.Add(job);
        ctx.SaveChanges();

        ctx.ExportTemplates.Remove(template);
        ctx.SaveChanges();

        // ExportJob should still exist but ExportTemplateId should be NULL (SET NULL)
        var reloadedJob = ctx.ExportJobs.Single(j => j.Id == job.Id);
        Assert.Null(reloadedJob.ExportTemplateId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RESTRICT rules (sample)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HabitNode_to_HabitConfig_is_RESTRICT()
    {
        Guid nodeId;

        // Phase 1: insert data via EF, then dispose context to release the connection.
        using (var ctx = BuildContext())
        {
            EnableForeignKeys(ctx);
            var node = MakeHabit();
            ctx.HabitNodes.Add(node);
            ctx.SaveChanges();
            nodeId = node.Id;

            var config = new HabitConfig
            {
                Id = Guid.NewGuid(),
                HabitNodeId = nodeId,
                ScheduleType = ScheduleType.Daily,
                StreakMode = StreakMode.Daily,
                StreakRecovery = StreakRecovery.None,
            };
            ctx.HabitConfigs.Add(config);
            ctx.SaveChanges();
        } // ctx is disposed here, releasing the SQLite connection.

        // Phase 2: attempt raw SQL delete on a fresh connection with FK enforcement.
        // NOTE: EF Core stores GUIDs as uppercase TEXT in SQLite — use ToString("D") then ToUpperInvariant().
        var nodeIdUpper = nodeId.ToString("D").ToUpperInvariant();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var fkCmd = conn.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
        fkCmd.ExecuteNonQuery();
        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = $"DELETE FROM habit_nodes WHERE id = '{nodeIdUpper}'";

        // Should throw SqliteException with FK constraint failed
        var ex = Assert.Throws<SqliteException>(() => delCmd.ExecuteNonQuery());
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HabitNode_to_Completion_is_RESTRICT()
    {
        Guid nodeId;

        // Phase 1: insert data via EF, then dispose context to release the connection.
        using (var ctx = BuildContext())
        {
            EnableForeignKeys(ctx);
            var node = MakeHabit();
            ctx.HabitNodes.Add(node);
            ctx.SaveChanges();
            nodeId = node.Id;

            var completion = new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = nodeId,
                CompletedAt = DateTime.UtcNow,
                CompletedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            ctx.Completions.Add(completion);
            ctx.SaveChanges();
        } // ctx is disposed here, releasing the SQLite connection.

        // Phase 2: attempt raw SQL delete on a fresh connection with FK enforcement.
        // NOTE: EF Core stores GUIDs as uppercase TEXT in SQLite — use uppercase format.
        var nodeIdUpper = nodeId.ToString("D").ToUpperInvariant();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var fkCmd = conn.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
        fkCmd.ExecuteNonQuery();
        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = $"DELETE FROM habit_nodes WHERE id = '{nodeIdUpper}'";

        // Should throw SqliteException with FK constraint failed
        var ex = Assert.Throws<SqliteException>(() => delCmd.ExecuteNonQuery());
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch (IOException) { /* best effort */ }
    }
}
