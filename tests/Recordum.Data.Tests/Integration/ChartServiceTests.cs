using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ChartServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedAsync()
    {
        var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" });
        await db.SaveChangesAsync();
        return (fx, habitId);
    }

    [Fact]
    public async Task Create_inside_plugin_scope_records_provenance()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new ChartService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<ChartService>.Instance);
        Guid id;
        using (fx.PluginContext.BeginScope("plug"))
            id = await svc.CreateAsync(new Chart { Name = "c" }, new[]
            {
                new ChartSeries { Id = Guid.NewGuid(), HabitNodeId = habitId, SortOrder = 0 },
            });
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal("plug", (await db.Charts.FindAsync(id))!.CreatedByPluginId);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceSeriesAsync_swaps_atomically()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new ChartService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<ChartService>.Instance);
        var chartId = await svc.CreateAsync(new Chart { Name = "c" }, new[]
        {
            new ChartSeries { Id = Guid.NewGuid(), HabitNodeId = habitId, SortOrder = 0 },
        });
        await svc.ReplaceSeriesAsync(chartId, new[]
        {
            new ChartSeries { Id = Guid.NewGuid(), HabitNodeId = habitId, SortOrder = 0, Label = "renamed" },
        });
        await using var db = await fx.Factory.CreateDbContextAsync();
        var s = await db.ChartSeries.Where(x => x.ChartId == chartId).SingleAsync();
        Assert.Equal("renamed", s.Label);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListAsync_returns_all_charts_without_series_ordered_by_name()
    {
        var (fx, habitId) = await SeedAsync();
        var svc = new ChartService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<ChartService>.Instance);
        await svc.CreateAsync(new Chart { Name = "Beta", ChartType = "bar" });
        await svc.CreateAsync(new Chart { Name = "Alpha", ChartType = "line" }, new[]
        {
            new ChartSeries { Id = Guid.NewGuid(), HabitNodeId = habitId, SortOrder = 0 },
        });
        var all = await svc.ListAsync();
        Assert.Equal(new[] { "Alpha", "Beta" }, all.Select(c => c.Name).ToArray());
        await fx.DisposeAsync();
    }
}
