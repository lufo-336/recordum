using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Charts;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public sealed class ChartQueryServiceTests
{
    private static ChartQueryService Build(DbFixture fx) =>
        new(fx.Factory,
            fx.Clock,
            new TrendQueryService(fx.Factory, new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance)),
            new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus()),
            new MetricDefinitionService(fx.Factory, fx.Clock, NullLogger<MetricDefinitionService>.Instance));

    [Fact]
    public async Task BuildModel_action_habit_no_metric_uses_count_per_bucket()
    {
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero)));
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Run" });
            long Ms(int d) => new DateTimeOffset(2026, 5, d, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) });
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(20) + 1 });
            db.Completions.Add(new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = new DateTime(2026, 5, 21, 9, 0, 0, DateTimeKind.Utc), CompletedAtMs = Ms(21) });
            await db.SaveChangesAsync();
        }
        var chartSvc = new ChartService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<ChartService>.Instance);
        var chartId = await chartSvc.CreateAsync(
            new Chart { Name = "Runs", ChartType = "bar", ConfigJson = new ChartConfig(Range: "30d", Bucket: "daily").ToJson() },
            new[] { new ChartSeries { Id = Guid.NewGuid(), HabitNodeId = habitId, Label = "Runs", SortOrder = 0 } });

        var model = await Build(fx).BuildModelAsync(chartId, isDark: false);

        Assert.Equal(ChartKind.Bar, model.Kind);
        Assert.Single(model.Series);
        var pts = model.Series[0].Points;
        Assert.Equal(2d, pts.Single(p => p.Label == "2026-05-20").Y);
        Assert.Equal(1d, pts.Single(p => p.Label == "2026-05-21").Y);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task BuildModel_missing_chart_returns_empty_model()
    {
        var fx = new DbFixture();
        var model = await Build(fx).BuildModelAsync(Guid.NewGuid(), isDark: true);
        Assert.Empty(model.Series);
        Assert.True(model.Theme.IsDark);
        await fx.DisposeAsync();
    }
}
