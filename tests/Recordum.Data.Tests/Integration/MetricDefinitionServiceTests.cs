using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
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
    public async Task A_metric_with_a_scale_is_always_saved_as_decimal_on_create_and_update()
    {
        await using var fx = new DbFixture();
        var hid = Guid.NewGuid();
        Guid scaleId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = hid, NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" });
            scaleId = (await db.ScaleTemplates.FirstAsync()).Id;
            await db.SaveChangesAsync();
        }
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new RecordingEventBus());
        var withScale = await svc.CreateAsync(new MetricDefinition { HabitNodeId = hid, Name = "a", Type = MetricType.Integer, ScaleTemplateId = scaleId });
        var plain = await svc.CreateAsync(new MetricDefinition { HabitNodeId = hid, Name = "b", Type = MetricType.Integer });
        await svc.UpdateAsync(plain, m => m.ScaleTemplateId = scaleId);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(MetricType.Decimal, (await db2.MetricDefinitions.FindAsync(withScale))!.Type);
        Assert.Equal(MetricType.Decimal, (await db2.MetricDefinitions.FindAsync(plain))!.Type);
    }

    // GIO-23 review finale: cambiare scala a una metrica e' come stringere la sua (spec §6): i valori gia' registrati
    // devono stare nella scala di destinazione, altrimenti si rifiuta con lo stesso codice di ScaleRules.EditErrors.
    [Theory]
    [InlineData(1, 5, ScalePrecision.Decimal, 80, "narrow-max")]
    [InlineData(90, 100, ScalePrecision.Decimal, 80, "narrow-min")]
    [InlineData(0, 100, ScalePrecision.Integer, 1.37, "precision")]
    public async Task Changing_scale_is_refused_when_recorded_values_do_not_fit_the_target(
        int min, int max, ScalePrecision precision, double recorded, string code)
    {
        await using var fx = new DbFixture();
        var scales = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var from = await scales.CreateAsync(new("Da", 0, 100, ScalePrecision.Decimal, []));
        var to = await scales.CreateAsync(new("A", min, max, precision, []));
        var metricId = await StateMetricWithValueAsync(fx, from, (decimal)recorded);
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new RecordingEventBus());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.UpdateAsync(metricId, m => m.ScaleTemplateId = to));

        Assert.Equal(code, ex.Message);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(from, (await db.MetricDefinitions.FindAsync(metricId))!.ScaleTemplateId);
    }

    [Fact]
    public async Task Changing_scale_to_one_that_contains_the_recorded_values_is_saved()
    {
        await using var fx = new DbFixture();
        var scales = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var from = await scales.CreateAsync(new("Da", 0, 100, ScalePrecision.Decimal, []));
        var to = await scales.CreateAsync(new("A", 50, 90, ScalePrecision.Integer, []));
        var metricId = await StateMetricWithValueAsync(fx, from, 80m);
        var svc = new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance, new RecordingEventBus());

        await svc.UpdateAsync(metricId, m => m.ScaleTemplateId = to);

        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(to, (await db.MetricDefinitions.FindAsync(metricId))!.ScaleTemplateId);
    }

    private static async Task<Guid> StateMetricWithValueAsync(DbFixture fx, Guid scaleId, decimal value)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" };
        var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habit.Id, Name = "M", Type = MetricType.Decimal, ScaleTemplateId = scaleId };
        var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = habit.Id, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 };
        db.HabitNodes.Add(habit);
        db.MetricDefinitions.Add(metric);
        db.Completions.Add(c);
        db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = c.Id, MetricDefinitionId = metric.Id, ValueDecimal = value });
        await db.SaveChangesAsync();
        return metric.Id;
    }
}
