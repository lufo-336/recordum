using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ComposedHabitServiceTests
{
    [Fact]
    public async Task CreateComposedHabit_persists_node_config_and_metrics_atomically()
    {
        await using var fx = new DbFixture();
        var svc = new ComposedHabitService(fx.Factory);
        var nodeId = Guid.NewGuid();
        var node = new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "Flessioni", Color = "#5fb37a" };
        var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Ripetizioni", Type = MetricType.Integer, SortOrder = 0 };
        var req = new ComposedHabitRequest(node, Config: null, Metrics: new[] { metric });

        var returned = await svc.CreateComposedHabitAsync(req);

        returned.Should().Be(nodeId);
        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.HabitNodes.FindAsync(nodeId)).Should().NotBeNull();
        (await db.MetricDefinitions.CountAsync(m => m.HabitNodeId == nodeId)).Should().Be(1);
    }

    [Fact]
    public async Task CreateComposedHabit_rolls_back_on_failure()
    {
        await using var fx = new DbFixture();
        var svc = new ComposedHabitService(fx.Factory);
        var nodeId = Guid.NewGuid();
        var node = new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "X", Color = "#808080" };
        // Trigger a guaranteed failure mid-save: two metrics sharing the SAME primary key
        // makes SaveChanges throw on the duplicate insert. The transaction must roll back,
        // leaving no orphan node persisted.
        var dupId = Guid.NewGuid();
        var m1 = new MetricDefinition { Id = dupId, HabitNodeId = nodeId, Name = "A", Type = MetricType.Integer, SortOrder = 0 };
        var m2 = new MetricDefinition { Id = dupId, HabitNodeId = nodeId, Name = "B", Type = MetricType.Integer, SortOrder = 1 };
        var req = new ComposedHabitRequest(node, null, new[] { m1, m2 });

        var act = async () => await svc.CreateComposedHabitAsync(req);

        await act.Should().ThrowAsync<Exception>();
        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.HabitNodes.FindAsync(nodeId)).Should().BeNull();
    }

    [Fact]
    public async Task CreateComposedHabitAsync_materializes_everything_in_one_transaction()
    {
        await using var fx = new DbFixture();
        var svc = new ComposedHabitService(fx.Factory);
        var nodeId = Guid.NewGuid();
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "tazze", Name = "Tazze", IsOfficial = false };
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = "energia-3", Name = "Energia",
            MinValue = 1, MaxValue = 3, IsOfficial = false,
            Anchors = [new ScaleAnchor { Value = 1, Label = "Bassa" }, new ScaleAnchor { Value = 3, Label = "Alta" }] };
        var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Tazze",
            Type = MetricType.Decimal, UnitOfMeasureId = unit.Id, SortOrder = 0 };
        var request = new ComposedHabitRequest(
            Node: new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Tè", Color = "#123456" },
            Config: new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = nodeId, Repeatable = true,
                ScheduleType = ScheduleType.SpecificDays, ScheduleDaysJson = """["Mon","Fri"]""",
                StreakMode = StreakMode.Weekly, StreakRecovery = StreakRecovery.None,
                SeriesLabelsJson = """["Mattina","Sera"]""" },
            Metrics: [metric],
            Segments: [new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Work", IsRest = false, SortOrder = 0 }],
            Goal: new Goal { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Tè",
                MetricDefinitionId = metric.Id, TargetValue = 3, PeriodType = GoalPeriodType.Open },
            NewUnits: [unit],
            NewScales: [scale],
            UiPrefs: new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });

        var id = await svc.CreateComposedHabitAsync(request);

        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.UnitsOfMeasure.AnyAsync(u => u.Code == "tazze")).Should().BeTrue();
        (await db.ScaleTemplates.AnyAsync(s => s.Code == "energia-3")).Should().BeTrue();
        (await db.SegmentDefinitions.CountAsync(s => s.HabitNodeId == id)).Should().Be(1);
        (await db.Goals.SingleAsync(g => g.HabitNodeId == id)).TargetValue.Should().Be(3);
        var node = await db.HabitNodes.SingleAsync(n => n.Id == id);
        HabitUiPrefsJson.Deserialize(node.UiPrefsJson).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
    }

    [Fact]
    public async Task CreateComposedHabitAsync_with_minimal_request_behaves_as_before()
    {
        // Compat: i 3 argomenti storici bastano (default null sui nuovi) — il call site legacy compila e crea solo nodo+config+metriche.
        await using var fx = new DbFixture();
        var svc = new ComposedHabitService(fx.Factory);
        var nodeId = Guid.NewGuid();
        var id = await svc.CreateComposedHabitAsync(new ComposedHabitRequest(
            new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "A", Color = "#000000" },
            new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = nodeId }, []));
        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.HabitNodes.AnyAsync(n => n.Id == id)).Should().BeTrue();
        (await db.Goals.AnyAsync(g => g.HabitNodeId == id)).Should().BeFalse();
    }
}
