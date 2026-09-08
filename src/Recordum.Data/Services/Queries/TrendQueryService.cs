using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Time;

namespace Recordum.Data.Services.Queries;

public sealed class TrendQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IMetricDefinitionService metrics,
    IResetTimeResolver resetTimes) : ITrendQueryService
{
    public async ValueTask<IReadOnlyList<TrendPoint>> GetTrendAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, TrendGranularity granularity, CancellationToken ct = default)
    {
        var day = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
        var (from, toExclusive) = day.RangeOf(fromDate, toDate);
        var fromMs = from.ToUnixTimeMilliseconds();
        var toMs = toExclusive.ToUnixTimeMilliseconds();

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var habit = await db.HabitNodes.AsNoTracking().FirstOrDefaultAsync(h => h.Id == habitNodeId, ct).ConfigureAwait(false);
        if (habit is null) return Array.Empty<TrendPoint>();

        var primary = (await metrics.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(false))
            .OrderBy(m => m.SortOrder).FirstOrDefault();

        var completions = await db.Completions.AsNoTracking()
            .Where(c => c.HabitNodeId == habitNodeId && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
            .Select(c => new CompletionProjection(
                c.CompletedAtMs,
                c.Values.Select(v => new ValueProjection(v.MetricDefinitionId, v.ValueInteger, v.ValueDecimal, v.ValueSeconds)).ToList()))
            .ToListAsync(ct).ConfigureAwait(false);

        if (completions.Count == 0) return Array.Empty<TrendPoint>();

        return completions
            .GroupBy(c => BucketKey(day.DayOf(c.CompletedAtMs), granularity))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var rows = g.ToList();
                var count = rows.Count;
                decimal value = ComputeValue(habit.HabitType, primary?.Id, rows, count);
                return new TrendPoint(g.Key, value, count);
            })
            .ToList();
    }

    private static decimal ComputeValue(HabitType? type, Guid? primaryMetricId, List<CompletionProjection> rows, int count)
    {
        if (type is null or HabitType.Action or HabitType.Journal || primaryMetricId is null) return count;

        var values = rows
            .SelectMany(r => r.Values)
            .Where(v => v.MetricDefinitionId == primaryMetricId.Value)
            .ToList();

        if (type == HabitType.State)
        {
            var ints = values.Where(v => v.ValueInteger.HasValue).Select(v => (decimal)v.ValueInteger!.Value).ToList();
            return ints.Count == 0 ? 0m : ints.Sum() / ints.Count;
        }

        return values.Sum(v =>
            v.ValueDecimal
            ?? (decimal?)v.ValueInteger
            ?? (decimal?)v.ValueSeconds
            ?? 0m);
    }

    private static DateOnly BucketKey(DateOnly date, TrendGranularity g) => g switch
    {
        TrendGranularity.Daily => date,
        TrendGranularity.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        _ => date,
    };

    private sealed record CompletionProjection(long CompletedAtMs, List<ValueProjection> Values);
    private sealed record ValueProjection(Guid MetricDefinitionId, long? ValueInteger, decimal? ValueDecimal, int? ValueSeconds);
}
