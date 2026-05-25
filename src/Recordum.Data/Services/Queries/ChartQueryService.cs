using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Data.Services.Queries;

public sealed class ChartQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ITrendQueryService trends,
    ICompletionService completions,
    IMetricDefinitionService metrics) : IChartQueryService
{
    // metrics is reserved for Task 7 (metric-label resolution on series points)
    private readonly IMetricDefinitionService _metrics = metrics;

    private const string DarkText = "#E6E6E6";
    private const string DarkGrid = "#333333";
    private const string LightText = "#202020";
    private const string LightGrid = "#E0E0E0";

    public async ValueTask<ChartRenderModel> BuildModelAsync(Guid chartId, bool isDark, CancellationToken ct = default)
    {
        var theme = new ChartThemeModel(isDark, isDark ? DarkText : LightText, isDark ? DarkGrid : LightGrid);

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var chart = await db.Charts.AsNoTracking()
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.Id == chartId, ct).ConfigureAwait(false);
        if (chart is null)
            return new ChartRenderModel(ChartKind.Line, null, [], null, null, theme);

        var kind = ChartKindParser.Parse(chart.ChartType);
        var config = ChartConfig.Parse(chart.ConfigJson);
        var (from, to) = ResolveRange(config);
        var granularity = config.Bucket.Equals("weekly", StringComparison.OrdinalIgnoreCase)
            ? TrendGranularity.Weekly : TrendGranularity.Daily;

        var series = new List<ChartSeriesModel>();
        foreach (var s in chart.Series.OrderBy(s => s.SortOrder))
        {
            var points = await ResolveSeriesPointsAsync(s.HabitNodeId, s.MetricDefinitionId, from, to, granularity, ct)
                .ConfigureAwait(false);
            series.Add(new ChartSeriesModel(s.Label ?? "", s.Color, points));
        }

        var xAxis = new ChartAxisModel(config.XAxisLabel, IsTime: true, Format: "yyyy-MM-dd");
        var yAxis = new ChartAxisModel(config.YAxisLabel, IsTime: false, Format: null);
        return new ChartRenderModel(kind, chart.Name, series, xAxis, yAxis, theme);
    }

    private (DateOnly from, DateOnly to) ResolveRange(ChartConfig config)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return config.Range switch
        {
            "30d" => (today.AddDays(-29), today),
            "1y"  => (today.AddDays(-364), today),
            "custom" when TryDate(config.FromIso, out var f) && TryDate(config.ToIso, out var t) => (f, t),
            _     => (today.AddDays(-89), today), // "90d" and any fallback
        };

        static bool TryDate(string? iso, out DateOnly d)
            => DateOnly.TryParse(iso, CultureInfo.InvariantCulture, out d);
    }

    private async Task<IReadOnlyList<ChartPoint>> ResolveSeriesPointsAsync(
        Guid habitNodeId, Guid? metricId, DateOnly from, DateOnly to, TrendGranularity granularity, CancellationToken ct)
    {
        if (metricId is { } mid)
        {
            var daily = await completions.GetDailySumAsync(habitNodeId, mid, from, to, ct).ConfigureAwait(false);
            var buckets = daily.GroupBy(d => BucketKey(d.Date, granularity))
                .OrderBy(g => g.Key)
                .Select(g => new ChartPoint(DayNumber(g.Key), (double)g.Sum(x => x.Sum), g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            return buckets.ToList();
        }

        var trend = await trends.GetTrendAsync(habitNodeId, from, to, granularity, ct).ConfigureAwait(false);
        return trend
            .Select(p => new ChartPoint(DayNumber(p.Bucket), (double)p.Value, p.Bucket.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static DateOnly BucketKey(DateOnly date, TrendGranularity g) => g switch
    {
        TrendGranularity.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        _                       => date,
    };

    private static double DayNumber(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToOADate();
}
