using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class MetricDefinitionServiceTests
{
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
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance);
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
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance);
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
