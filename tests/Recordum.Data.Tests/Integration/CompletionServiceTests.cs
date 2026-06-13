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
    public async Task Record_bare_counter_occurrence_succeeds()
    {
        // Spec multivalue §4.1: a bare counter tap (+1, no values) is a valid occurrence.
        var (fx, habitId) = await SeedActionAsync(HabitType.Counter);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var id = await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        Assert.NotEqual(Guid.Empty, id);
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

    private static long MsAt(DateOnly d, int hour) =>
        new DateTimeOffset(d.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static DateTime UtcAt(DateOnly d, int hour) =>
        DateTime.SpecifyKind(d.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

    [Fact]
    public async Task GetDailySumAsync_sums_completion_values_for_given_metric_per_day()
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
        var day1 = new DateOnly(2026, 1, 5);
        var day2 = new DateOnly(2026, 1, 6);
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day1, 9), CompletedAtMs = MsAt(day1, 9) },
            new[] { new CompletionValue { MetricDefinitionId = metricId, ValueInteger = 10 } });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day1, 12), CompletedAtMs = MsAt(day1, 12) },
            new[] { new CompletionValue { MetricDefinitionId = metricId, ValueInteger = 15 } });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day2, 9), CompletedAtMs = MsAt(day2, 9) },
            new[] { new CompletionValue { MetricDefinitionId = metricId, ValueInteger = 7 } });

        var rows = await svc.GetDailySumAsync(habitId, metricId, day1, day2);

        Assert.Equal(2, rows.Count);
        Assert.Equal(25m, rows.Single(r => r.Date == day1).Sum);
        Assert.Equal(7m, rows.Single(r => r.Date == day2).Sum);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetHeatmapAsync_breaks_down_retroactive_count()
    {
        var (fx, habitId) = await SeedActionAsync();
        var day = new DateOnly(2026, 5, 10);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.Completions.AddRange(
                new Completion
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = habitId,
                    CompletedAt = UtcAt(day, 9),
                    CompletedAtMs = MsAt(day, 9),
                    IsRetroactive = false,
                },
                new Completion
                {
                    Id = Guid.NewGuid(),
                    HabitNodeId = habitId,
                    CompletedAt = UtcAt(day, 10),
                    CompletedAtMs = MsAt(day, 10),
                    IsRetroactive = true,
                });
            await db.SaveChangesAsync();
        }
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());

        var cells = await svc.GetHeatmapAsync(habitId, day, day);

        var cell = cells.Should().ContainSingle().Subject;
        cell.Count.Should().Be(2);
        cell.RetroactiveCount.Should().Be(1);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetDailySumAsync_ignores_values_for_other_metrics()
    {
        var (fx, habitId) = await SeedActionAsync(HabitType.Counter);
        Guid primaryMetricId, secondaryMetricId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var primary = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "reps", Type = MetricType.Integer, SortOrder = 0 };
            var secondary = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "weight", Type = MetricType.Decimal, SortOrder = 1 };
            db.MetricDefinitions.AddRange(primary, secondary);
            await db.SaveChangesAsync();
            primaryMetricId = primary.Id;
            secondaryMetricId = secondary.Id;
        }
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var day = new DateOnly(2026, 1, 5);
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 9), CompletedAtMs = MsAt(day, 9) },
            new[]
            {
                new CompletionValue { MetricDefinitionId = primaryMetricId, ValueInteger = 10 },
                new CompletionValue { MetricDefinitionId = secondaryMetricId, ValueDecimal = 100m, SeriesIndex = 0 },
            });

        var rows = await svc.GetDailySumAsync(habitId, primaryMetricId, day, day);

        Assert.Equal(10m, rows.Single().Sum);
        await fx.DisposeAsync();
    }

    // ── GetDailyMagnitudeAsync tests ─────────────────────────────────────────

    private static async Task<(DbFixture fx, Guid habitId, Guid? metricId)> SeedHabitWithMetricAsync(HabitType type)
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = type, Name = "h" };
        db.HabitNodes.Add(h);
        Guid? metricId = null;
        if (type is HabitType.Counter or HabitType.Resistance or HabitType.State)
        {
            var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = h.Id, Name = "primary", Type = MetricType.Integer, SortOrder = 0 };
            db.MetricDefinitions.Add(metric);
            metricId = metric.Id;
        }
        await db.SaveChangesAsync();
        return (fx, h.Id, metricId);
    }

    [Fact]
    public async Task Magnitude_resistance_counts_distinct_series_within_one_completion()
    {
        var (fx, habitId, metricId) = await SeedHabitWithMetricAsync(HabitType.Resistance);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var day = new DateOnly(2026, 6, 2);
        // One completion with 3 series (SeriesIndex 0, 1, 2)
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 10), CompletedAtMs = MsAt(day, 10) },
            new[]
            {
                new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 0, ValueInteger = 10 },
                new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 1, ValueInteger = 10 },
                new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 2, ValueInteger = 10 },
            });

        var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

        Assert.Equal(3m, mags.Single(d => d.Date == day).Magnitude);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Magnitude_counter_counts_occurrences_not_metric_values()
    {
        var (fx, habitId, metricId) = await SeedHabitWithMetricAsync(HabitType.Counter);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var day = new DateOnly(2026, 6, 3);
        // Two single-series completions same day (values 60 and 40) → 2 occurrences, not 100.
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 9), CompletedAtMs = MsAt(day, 9) },
            new[] { new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 0, ValueInteger = 60 } });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 12), CompletedAtMs = MsAt(day, 12) },
            new[] { new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 0, ValueInteger = 40 } });

        var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

        Assert.Equal(2m, mags.Single(d => d.Date == day).Magnitude);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Counter_magnitude_counts_bare_occurrence_and_multi_series()
    {
        var (fx, habitId, metricId) = await SeedHabitWithMetricAsync(HabitType.Counter);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var day = new DateOnly(2026, 6, 7);
        // 1 bare completion (floor 1) + 1 completion with 3 distinct series → 1 + 3 = 4.
        await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 9), CompletedAtMs = MsAt(day, 9) });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 12), CompletedAtMs = MsAt(day, 12) },
            new[]
            {
                new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 0, ValueInteger = 10 },
                new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 1, ValueInteger = 11 },
                new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 2, ValueInteger = 12 },
            });

        var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

        Assert.Equal(4m, mags.Single(d => d.Date == day).Magnitude);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Magnitude_action_counts_completions()
    {
        var (fx, habitId, _) = await SeedHabitWithMetricAsync(HabitType.Action);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var day = new DateOnly(2026, 6, 4);
        // Two completions same day
        await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 8), CompletedAtMs = MsAt(day, 8) });
        await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 9), CompletedAtMs = MsAt(day, 9) });

        var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

        Assert.Equal(2m, mags.Single(d => d.Date == day).Magnitude);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Magnitude_state_takes_latest_value()
    {
        var (fx, habitId, metricId) = await SeedHabitWithMetricAsync(HabitType.State);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var day = new DateOnly(2026, 6, 5);
        // Two completions same day: value 2 (hour 8) and value 4 (hour 21, larger CompletedAtMs)
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 8), CompletedAtMs = MsAt(day, 8) },
            new[] { new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 0, ValueInteger = 2 } });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(day, 21), CompletedAtMs = MsAt(day, 21) },
            new[] { new CompletionValue { MetricDefinitionId = metricId!.Value, SeriesIndex = 0, ValueInteger = 4 } });

        var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

        Assert.Equal(4m, mags.Single(d => d.Date == day).Magnitude);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Magnitude_excludes_other_months_and_empty_days()
    {
        var (fx, habitId, _) = await SeedHabitWithMetricAsync(HabitType.Action);
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var dayJuly = new DateOnly(2026, 7, 1);
        // Completion in July, query for June → empty
        await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = UtcAt(dayJuly, 9), CompletedAtMs = MsAt(dayJuly, 9) });

        var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

        Assert.Empty(mags);
        await fx.DisposeAsync();
    }
}
