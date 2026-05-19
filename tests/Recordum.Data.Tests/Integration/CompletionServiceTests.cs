using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class CompletionServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedActionAsync(HabitType type = HabitType.Action)
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = type, Name = "h" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    [Fact]
    public async Task Record_action_no_values_succeeds()
    {
        var (fx, habitId) = await SeedActionAsync();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance);
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        Assert.NotEqual(Guid.Empty, id);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Record_counter_without_values_throws()
    {
        var (fx, habitId) = await SeedActionAsync(HabitType.Counter);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance);
        await Assert.ThrowsAsync<InvalidCompletionForHabitTypeException>(() =>
            svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 }).AsTask());
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Record_counter_with_value_persists_value()
    {
        var (fx, habitId) = await SeedActionAsync(HabitType.Counter);
        Guid metricId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "reps", Type = MetricType.Integer };
            db.MetricDefinitions.Add(metric);
            await db.SaveChangesAsync();
            metricId = metric.Id;
        }
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance);
        var id = await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 },
            new[] { new CompletionValue { Id = Guid.NewGuid(), MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = 10 } });
        await using var read = await fx.Factory.CreateDbContextAsync();
        var c = await read.Completions.Include(x => x.Values).SingleAsync(x => x.Id == id);
        Assert.Single(c.Values);
        Assert.Equal(10, c.Values[0].ValueInteger);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Update_retroactive_flag_persists()
    {
        var (fx, habitId) = await SeedActionAsync();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance);
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        await svc.UpdateAsync(id, c => c.IsRetroactive = true);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.True((await db.Completions.FindAsync(id))!.IsRetroactive);
        await fx.DisposeAsync();
    }
}
