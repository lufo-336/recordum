using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
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
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        Assert.NotEqual(Guid.Empty, id);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Record_counter_without_values_throws()
    {
        var (fx, habitId) = await SeedActionAsync(HabitType.Counter);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
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
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
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
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        await svc.UpdateAsync(id, c => c.IsRetroactive = true);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.True((await db.Completions.FindAsync(id))!.IsRetroactive);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task RecordAsync_publishes_CompletionRecorded()
    {
        var (fx, habitId) = await SeedActionAsync();
        var bus = new RecordingEventBus();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus);

        var newId = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = fx.Clock.UtcNow.UtcDateTime, CompletedAtMs = fx.Clock.UtcNow.ToUnixTimeMilliseconds() });

        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<CompletionRecordedPayload>()
            .Which.CompletionId.Should().Be(newId);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_publishes_CompletionUpdated_when_row_mutated()
    {
        var (fx, habitId) = await SeedActionAsync();
        var bus = new RecordingEventBus();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus);
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = fx.Clock.UtcNow.UtcDateTime, CompletedAtMs = 1 });
        bus.Published.Clear();

        await svc.UpdateAsync(id, c => c.IsRetroactive = true);

        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<CompletionUpdatedPayload>()
            .Which.CompletionId.Should().Be(id);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_does_not_publish_when_id_not_found()
    {
        var (fx, _) = await SeedActionAsync();
        var bus = new RecordingEventBus();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus);

        await svc.UpdateAsync(Guid.NewGuid(), c => c.IsRetroactive = true);

        bus.Published.Should().BeEmpty();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_publishes_CompletionDeleted()
    {
        var (fx, habitId) = await SeedActionAsync();
        var bus = new RecordingEventBus();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus);
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = fx.Clock.UtcNow.UtcDateTime, CompletedAtMs = 1 });
        bus.Published.Clear();

        await svc.DeleteAsync(id);

        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<CompletionDeletedPayload>()
            .Which.CompletionId.Should().Be(id);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_no_publish_when_id_not_found()
    {
        var (fx, _) = await SeedActionAsync();
        var bus = new RecordingEventBus();
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, bus);

        await svc.DeleteAsync(Guid.NewGuid());

        bus.Published.Should().BeEmpty();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListByHabitAsync_filters_by_range_and_orders_desc()
    {
        var (fx, habitId) = await SeedActionAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            for (int i = 0; i < 5; i++)
            {
                var ts = new DateTime(2026, 5, 10 + i, 12, 0, 0, DateTimeKind.Utc);
                db.Completions.Add(new Completion
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = habitId,
                    CompletedAt = ts,
                    CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
                });
            }
            await db.SaveChangesAsync();
        }
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());

        var result = await svc.ListByHabitAsync(
            habitId,
            fromUtc: new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 13, 23, 59, 59, TimeSpan.Zero),
            take: 10);

        result.Select(c => c.CompletedAt.Day).Should().Equal(13, 12, 11);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListByHabitAsync_take_caps_result_size()
    {
        var (fx, habitId) = await SeedActionAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            for (int i = 0; i < 5; i++)
            {
                var ts = new DateTime(2026, 5, 10 + i, 12, 0, 0, DateTimeKind.Utc);
                db.Completions.Add(new Completion
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = habitId,
                    CompletedAt = ts,
                    CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
                });
            }
            await db.SaveChangesAsync();
        }
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());

        var result = await svc.ListByHabitAsync(habitId, take: 2);

        result.Should().HaveCount(2);
        result.Select(c => c.CompletedAt.Day).Should().Equal(14, 13);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetHeatmapAsync_aggregates_by_day_count()
    {
        var (fx, habitId) = await SeedActionAsync(HabitType.Counter);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.Completions.AddRange(
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc), CompletedAtMs = new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = new DateTimeOffset(2026, 5, 10, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() },
                new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = new DateTimeOffset(2026, 5, 11, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() });
            await db.SaveChangesAsync();
        }
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());

        var cells = await svc.GetHeatmapAsync(habitId, new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 12));

        cells.Should().BeEquivalentTo(new[]
        {
            new HeatmapCell(new DateOnly(2026, 5, 10), 2),
            new HeatmapCell(new DateOnly(2026, 5, 11), 1),
        });
        await fx.DisposeAsync();
    }
}
