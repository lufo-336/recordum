using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Data.Services.Queries;

public sealed class HabitTreeQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock) : IHabitTreeQueryService
{
    public ValueTask<IReadOnlyList<HomeTreeRow>> GetHomeTreeAsync(CancellationToken ct = default)
        => BuildRowsAsync(n => n.DeletedAt == null, ct);

    public ValueTask<IReadOnlyList<HomeTreeRow>> GetTrashTreeAsync(CancellationToken ct = default)
        => BuildRowsAsync(n => n.DeletedAt != null, ct);

    private async ValueTask<IReadOnlyList<HomeTreeRow>> BuildRowsAsync(
        Expression<Func<HabitNode, bool>> predicate,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = clock.UtcNow.UtcDateTime.Date;
        var startMs = new DateTimeOffset(today.AddDays(-13), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(today.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var todayMs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var dayBuckets = await db.Completions
            .AsNoTracking()
            .Where(c => c.CompletedAtMs >= startMs && c.CompletedAtMs < endMs)
            .GroupBy(c => new
            {
                c.HabitNodeId,
                DayOffset = (int)((c.CompletedAtMs - startMs) / 86_400_000),
            })
            .Select(g => new { g.Key.HabitNodeId, g.Key.DayOffset, Count = g.Count() })
            .ToListAsync(ct);

        var bucketsByHabit = dayBuckets
            .GroupBy(b => b.HabitNodeId)
            .ToDictionary(g => g.Key, g =>
            {
                var arr = new int[14];
                foreach (var b in g)
                {
                    if (b.DayOffset is >= 0 and < 14) arr[b.DayOffset] = b.Count;
                }
                return ImmutableArray.Create(arr);
            });

        var emptyHeatmap14 = ImmutableArray.Create(new int[14]);

        var tagLinks = await db.HabitNodeTags
            .AsNoTracking()
            .Select(t => new { t.HabitNodeId, t.TagId })
            .ToListAsync(ct);
        var tagsByHabit = tagLinks
            .GroupBy(t => t.HabitNodeId)
            .ToDictionary(g => g.Key, g => ImmutableArray.CreateRange(g.Select(x => x.TagId)));

        // Primary metric per node (lowest SortOrder) — drives counter total/unit and state value/label.
        var metricRows = await db.MetricDefinitions
            .AsNoTracking()
            .Select(m => new
            {
                m.Id,
                m.HabitNodeId,
                m.SortOrder,
                m.Type,
                m.LabelsJson,
                m.ScaleTemplateId,
                UnitCode = m.UnitOfMeasure != null ? m.UnitOfMeasure.Code : null,
            })
            .ToListAsync(ct);
        var primaryMetricByHabit = metricRows
            .GroupBy(m => m.HabitNodeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.SortOrder).First());
        var primaryMetricIds = primaryMetricByHabit.Values.Select(m => m.Id).ToHashSet();

        // Shared scale-template labels — the fallback when a metric has no own LabelsJson override
        // (metric override wins, CSS-style specificity — matches MetricCellFactory/CompletionList precedent).
        var templateLabelsById = await db.ScaleTemplates
            .AsNoTracking()
            .Select(s => new { s.Id, s.LabelsJson })
            .ToDictionaryAsync(s => s.Id, s => s.LabelsJson, ct);

        // CompletionValues on each node's primary metric over the whole 14-day window — feeds the
        // counter total / state value of today AND the per-day value strip (2.1-bis: counter
        // mini-bars need daily sums, state ramp cells need the day's last value).
        var windowValueRows = await db.CompletionValues
            .AsNoTracking()
            .Where(cv => cv.Completion.CompletedAtMs >= startMs && cv.Completion.CompletedAtMs < endMs
                         && primaryMetricIds.Contains(cv.MetricDefinitionId))
            .Select(cv => new
            {
                cv.Completion.HabitNodeId,
                cv.Completion.CompletedAtMs,
                cv.ValueDecimal,
                cv.ValueInteger,
                cv.ValueSeconds,
            })
            .ToListAsync(ct);
        var todayValueRows = windowValueRows.Where(r => r.CompletedAtMs >= todayMs).ToList();
        var counterTotalsByHabit = todayValueRows
            .GroupBy(r => r.HabitNodeId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(r => r.ValueDecimal ?? (decimal?)r.ValueInteger ?? (decimal?)r.ValueSeconds ?? 0m));
        var stateLatestByHabit = todayValueRows
            .GroupBy(r => r.HabitNodeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CompletedAtMs).First());

        // Per-day value arrays (index 0 = 13 days ago … 13 = today; null = no value that day).
        // Counter = sum of the day's values; State = the day's last value (point-in-time, last-wins).
        var counterDailySumsByHabit = windowValueRows
            .GroupBy(r => r.HabitNodeId)
            .ToDictionary(g => g.Key, g =>
            {
                var arr = new decimal?[14];
                foreach (var day in g.GroupBy(r => (int)((r.CompletedAtMs - startMs) / 86_400_000)))
                {
                    if (day.Key is >= 0 and < 14)
                        arr[day.Key] = day.Sum(r => r.ValueDecimal ?? (decimal?)r.ValueInteger ?? (decimal?)r.ValueSeconds ?? 0m);
                }
                return ImmutableArray.Create(arr);
            });
        var stateDailyLastByHabit = windowValueRows
            .GroupBy(r => r.HabitNodeId)
            .ToDictionary(g => g.Key, g =>
            {
                var arr = new decimal?[14];
                foreach (var day in g.GroupBy(r => (int)((r.CompletedAtMs - startMs) / 86_400_000)))
                {
                    if (day.Key is >= 0 and < 14)
                    {
                        var last = day.OrderByDescending(r => r.CompletedAtMs).First();
                        arr[day.Key] = last.ValueDecimal ?? (decimal?)last.ValueInteger ?? (decimal?)last.ValueSeconds;
                    }
                }
                return ImmutableArray.Create(arr);
            });

        // Resistance: open (Abstaining) or most-recent-closed (Relapsed) period per node.
        var abstinenceRows = await db.AbstinencePeriods
            .AsNoTracking()
            .Select(p => new { p.HabitNodeId, p.StartedAtMs, p.EndedAtMs })
            .ToListAsync(ct);
        var abstinenceByHabit = abstinenceRows
            .GroupBy(p => p.HabitNodeId)
            .ToDictionary(g => g.Key, g =>
            {
                var open = g.Where(p => p.EndedAtMs == null)
                    .OrderByDescending(p => p.StartedAtMs)
                    .FirstOrDefault();
                return open is not null
                    ? (Status: AbstinenceStatus.Abstaining, OpenSinceMs: (long?)open.StartedAtMs)
                    : (Status: AbstinenceStatus.Relapsed, OpenSinceMs: (long?)null);
            });

        // Resistance strip = 14-day "abstaining" band from the periods (completion buckets don't apply
        // to abstinence). A day is filled when any period covers it; an open period runs up to now.
        var nowMs = clock.UtcNowMs;
        var abstinenceBandByHabit = abstinenceRows
            .GroupBy(p => p.HabitNodeId)
            .ToDictionary(g => g.Key, g =>
            {
                var arr = new int[14];
                foreach (var p in g)
                {
                    var pStart = p.StartedAtMs;
                    var pEnd = p.EndedAtMs ?? nowMs;
                    for (var i = 0; i < 14; i++)
                    {
                        var dayStart = startMs + (long)i * 86_400_000;
                        var dayEnd = dayStart + 86_400_000;
                        if (pStart < dayEnd && pEnd >= dayStart) arr[i] = 4; // abstaining that day → full band step
                    }
                }
                return ImmutableArray.Create(arr);
            });

        var raw = await db.HabitNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(predicate)
            .Select(n => new
            {
                n.Id,
                n.ParentId,
                n.Name,
                n.SortOrder,
                n.Color,
                n.Icon,
                n.HabitType,
                n.PinScope,
                n.IsArchived,
                LastCompletionAt = db.Completions
                    .Where(c => c.HabitNodeId == n.Id)
                    .OrderByDescending(c => c.CompletedAtMs)
                    .Select(c => (DateTime?)c.CompletedAt)
                    .FirstOrDefault(),
                IsCompletedToday = db.Completions.Any(c => c.HabitNodeId == n.Id && c.CompletedAtMs >= todayMs),
                ChildCount = db.HabitNodes.Count(child => child.ParentId == n.Id && child.DeletedAt == null),
            })
            .ToListAsync(ct);

        return raw.Select(r =>
            {
                var completions14 = bucketsByHabit.TryGetValue(r.Id, out var arr14) ? arr14 : emptyHeatmap14;
                var completions7 = ImmutableArray.Create(completions14.Skip(7).ToArray());

                decimal? todayCounterTotal = null;
                string? todayCounterUnitCode = null;
                bool todayCounterIsDuration = false;
                int? todayStateValue = null;
                string? todayStateLabel = null;
                var abstinenceStatus = AbstinenceStatus.NotStarted;
                long? abstinenceOpenSinceMs = null;
                int? journalEntriesThisWeek = null;
                var values14 = default(ImmutableArray<decimal?>);

                switch (r.HabitType)
                {
                    case HabitType.Counter:
                        todayCounterTotal = counterTotalsByHabit.TryGetValue(r.Id, out var total) ? total : 0m;
                        if (counterDailySumsByHabit.TryGetValue(r.Id, out var sums)) values14 = sums;
                        if (primaryMetricByHabit.TryGetValue(r.Id, out var counterMetric))
                        {
                            todayCounterUnitCode = counterMetric.UnitCode;
                            // A duration (Seconds) metric stores value_seconds; the total must be shown
                            // in the metric's own unit (90s on a "minutes" metric = 1.5), not the raw
                            // second count with a "minutes" label (which read "90 minutes" for 90s).
                            todayCounterIsDuration = counterMetric.Type == MetricType.Seconds;
                            if (todayCounterIsDuration && todayCounterTotal is { } secs)
                                todayCounterTotal =
                                    Recordum.Core.Formatting.SecondsDisplay.ConvertToUnit((int)secs, counterMetric.UnitCode)
                                    ?? todayCounterTotal; // no time unit to convert into: stays raw seconds
                        }
                        else
                        {
                            // Bare counter (no metrics): there is nothing to sum, so the total is the
                            // occurrence count — each tap = 1. Same rule as CounterTodayContent and the
                            // day-summary strip; without it the row alone read 0 for a day of taps.
                            todayCounterTotal = completions14[13];
                        }
                        break;
                    case HabitType.State:
                        if (stateDailyLastByHabit.TryGetValue(r.Id, out var lastVals)) values14 = lastVals;
                        if (stateLatestByHabit.TryGetValue(r.Id, out var latest) && latest.ValueInteger is long v)
                        {
                            todayStateValue = (int)v;
                            if (primaryMetricByHabit.TryGetValue(r.Id, out var stateMetric))
                            {
                                var labelsJson = stateMetric.LabelsJson;
                                if (string.IsNullOrWhiteSpace(labelsJson)
                                    && stateMetric.ScaleTemplateId is Guid tid
                                    && templateLabelsById.TryGetValue(tid, out var tmplLabels))
                                {
                                    labelsJson = tmplLabels;
                                }
                                todayStateLabel = ResolveLabel(labelsJson, todayStateValue.Value);
                            }
                        }
                        break;
                    case HabitType.Resistance:
                        if (abstinenceByHabit.TryGetValue(r.Id, out var abst))
                        {
                            abstinenceStatus = abst.Status;
                            abstinenceOpenSinceMs = abst.OpenSinceMs;
                        }
                        if (abstinenceBandByHabit.TryGetValue(r.Id, out var band14))
                        {
                            completions14 = band14;
                            completions7 = ImmutableArray.Create(band14.Skip(7).ToArray());
                        }
                        break;
                    case HabitType.Journal:
                        journalEntriesThisWeek = completions7.Sum();
                        break;
                    case HabitType.Action:
                    default:
                        break;
                }

                return new HomeTreeRow(
                    HabitNodeId: r.Id,
                    ParentId: r.ParentId,
                    Name: r.Name,
                    ColorHex: r.Color,
                    HabitType: r.HabitType,
                    LastCompletionAt: r.LastCompletionAt,
                    CurrentStreakCount: 0,
                    IsCompletedToday: r.IsCompletedToday,
                    PinScope: r.PinScope,
                    ChildCount: r.ChildCount,
                    IsArchived: r.IsArchived,
                    CompletionsLast7Days: completions7,
                    TagIds: tagsByHabit.TryGetValue(r.Id, out var tids) ? tids : ImmutableArray<Guid>.Empty,
                    CompletionsLast14Days: completions14,
                    TodayCounterTotal: todayCounterTotal,
                    TodayCounterUnitCode: todayCounterUnitCode,
                    TodayStateValue: todayStateValue,
                    TodayStateLabel: todayStateLabel,
                    AbstinenceStatus: abstinenceStatus,
                    AbstinenceOpenSinceMs: abstinenceOpenSinceMs,
                    JournalEntriesThisWeek: journalEntriesThisWeek,
                    Icon: r.Icon,
                    SortOrder: r.SortOrder,
                    ValuesLast14Days: values14,
                    TodayCounterIsDuration: todayCounterIsDuration);
            })
            .ToList();
    }

    private static string? ResolveLabel(string? labelsJson, int value)
    {
        if (string.IsNullOrWhiteSpace(labelsJson)) return null;
        string[]? labels;
        try
        {
            labels = JsonSerializer.Deserialize<string[]>(labelsJson);
        }
        catch (JsonException)
        {
            return null;
        }
        return labels is not null && value >= 1 && value <= labels.Length
            ? labels[value - 1]
            : null;
    }
}
