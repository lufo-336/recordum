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
}
