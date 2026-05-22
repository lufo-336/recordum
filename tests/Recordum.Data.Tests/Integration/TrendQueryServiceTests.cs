using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public sealed class TrendQueryServiceTests
{
    private static async Task<(DbFixture fx, HabitNode habit, Guid? primaryMetricId)> SeedHabitOfType(HabitType type)
    {
        var fx = new DbFixture();
        Guid? primaryMetricId = null;
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = type, Name = "h" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            if (type is HabitType.Counter or HabitType.Resistance)
            {
                var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habit.Id, Name = "primary", Type = MetricType.Decimal, SortOrder = 0 };
                db.MetricDefinitions.Add(m);
                primaryMetricId = m.Id;
            }
            else if (type is HabitType.State)
            {
                var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habit.Id, Name = "level", Type = MetricType.Integer, SortOrder = 0 };
                db.MetricDefinitions.Add(m);
                primaryMetricId = m.Id;
            }
            await db.SaveChangesAsync();
        }
        return (fx, habit, primaryMetricId);
    }

    private static long MsAt(DateOnly d, int hour) =>
        new DateTimeOffset(d.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static DateTime UtcAt(DateOnly d, int hour) =>
        DateTime.SpecifyKind(d.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

    private static CompletionService NewCompletionService(DbFixture fx) =>
        new(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());

    private static MetricDefinitionService NewMetricService(DbFixture fx) =>
        new(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance);

    private static TrendQueryService NewSut(DbFixture fx) =>
        new(fx.Factory, NewMetricService(fx));

    [Fact]
    public async Task Counter_sums_primary_metric_per_day()
    {
        var (fx, habit, primaryMetricId) = await SeedHabitOfType(HabitType.Counter);
        var d = new DateOnly(2026, 1, 5);
        var svc = NewCompletionService(fx);
        await svc.RecordAsync(
            new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d, 9), CompletedAtMs = MsAt(d, 9) },
            new[] { new CompletionValue { MetricDefinitionId = primaryMetricId!.Value, ValueDecimal = 10m } });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d, 12), CompletedAtMs = MsAt(d, 12) },
            new[] { new CompletionValue { MetricDefinitionId = primaryMetricId.Value, ValueDecimal = 5m } });

        var rows = await NewSut(fx).GetTrendAsync(habit.Id, d, d, TrendGranularity.Daily);

        var row = Assert.Single(rows);
        Assert.Equal(15m, row.Value);
        Assert.Equal(2, row.Count);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Action_returns_count_per_day_value_equals_count()
    {
        var (fx, habit, _) = await SeedHabitOfType(HabitType.Action);
        var d = new DateOnly(2026, 1, 5);
        var svc = NewCompletionService(fx);
        for (int i = 0; i < 3; i++)
        {
            await svc.RecordAsync(new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d, 9 + i), CompletedAtMs = MsAt(d, 9 + i) });
        }

        var rows = await NewSut(fx).GetTrendAsync(habit.Id, d, d, TrendGranularity.Daily);

        var row = Assert.Single(rows);
        Assert.Equal(3m, row.Value);
        Assert.Equal(3, row.Count);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task State_averages_value_integer_per_day()
    {
        var (fx, habit, primaryMetricId) = await SeedHabitOfType(HabitType.State);
        var d = new DateOnly(2026, 1, 5);
        var svc = NewCompletionService(fx);
        await svc.RecordAsync(
            new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d, 9), CompletedAtMs = MsAt(d, 9) },
            new[] { new CompletionValue { MetricDefinitionId = primaryMetricId!.Value, ValueInteger = 2 } });
        await svc.RecordAsync(
            new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d, 12), CompletedAtMs = MsAt(d, 12) },
            new[] { new CompletionValue { MetricDefinitionId = primaryMetricId.Value, ValueInteger = 4 } });

        var rows = await NewSut(fx).GetTrendAsync(habit.Id, d, d, TrendGranularity.Daily);

        Assert.Equal(3m, rows.Single().Value);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Weekly_granularity_buckets_by_iso_week_monday()
    {
        var (fx, habit, _) = await SeedHabitOfType(HabitType.Action);
        var svc = NewCompletionService(fx);
        var d1 = new DateOnly(2026, 1, 5);
        var d2 = new DateOnly(2026, 1, 7);
        var d3 = new DateOnly(2026, 1, 12);
        await svc.RecordAsync(new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d1, 9), CompletedAtMs = MsAt(d1, 9) });
        await svc.RecordAsync(new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d2, 9), CompletedAtMs = MsAt(d2, 9) });
        await svc.RecordAsync(new Completion { HabitNodeId = habit.Id, CompletedAt = UtcAt(d3, 9), CompletedAtMs = MsAt(d3, 9) });

        var rows = await NewSut(fx).GetTrendAsync(habit.Id, d1, new DateOnly(2026, 1, 13), TrendGranularity.Weekly);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 1, 5), rows[0].Bucket);
        Assert.Equal(2, rows[0].Count);
        Assert.Equal(new DateOnly(2026, 1, 12), rows[1].Bucket);
        Assert.Equal(1, rows[1].Count);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Empty_range_returns_empty_list()
    {
        var (fx, habit, _) = await SeedHabitOfType(HabitType.Action);

        var rows = await NewSut(fx).GetTrendAsync(habit.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), TrendGranularity.Daily);

        Assert.Empty(rows);
        await fx.DisposeAsync();
    }
}
