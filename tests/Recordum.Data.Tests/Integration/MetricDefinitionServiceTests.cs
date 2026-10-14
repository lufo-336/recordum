using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class MetricDefinitionServiceTests
{
    [Fact]
    public async Task Create_update_and_delete_each_publish_HabitUpdated_for_the_habit()
    {
        await using var fx = new DbFixture();
        var hid = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = hid, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" });
            await db.SaveChangesAsync();
        }
        var bus = new RecordingEventBus();
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, bus);
        var id = await svc.CreateAsync(new MetricDefinition { HabitNodeId = hid, Name = "m", Type = MetricType.Integer });
        await svc.UpdateAsync(id, m => m.Type = MetricType.Decimal);
        await svc.DeleteAsync(id);
        Assert.Equal([hid, hid, hid], bus.Published.OfType<HabitUpdatedPayload>().Select(p => p.HabitNodeId).ToArray());
    }

    [Fact]
    public async Task LabelsJson_override_is_persisted()
    {
        await using var fx = new DbFixture();
        var hid = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = hid, NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" });
            await db.SaveChangesAsync();
        }
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var id = await svc.CreateAsync(new MetricDefinition
        {
            HabitNodeId = hid,
            Name = "mood",
            Type = MetricType.Integer,
            LabelsJson = "[\"awful\",\"meh\",\"ok\"]",
        });
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var m = await db2.MetricDefinitions.FindAsync(id);
        Assert.Contains("awful", m!.LabelsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_non_array_LabelsJson()
    {
        await using var fx = new DbFixture();
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new MetricDefinition
            {
                HabitNodeId = Guid.NewGuid(),
                Name = "x",
                Type = MetricType.Integer,
                LabelsJson = "{\"a\":1}",
            }).AsTask());
    }
}
