using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed partial class CompletionService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<CompletionService> logger,
    IEventBus eventBus) : ICompletionService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Completion {Id} recorded for habit {HabitId}")]
    private partial void LogRecorded(Guid id, Guid habitId);

    public async ValueTask<Guid> RecordAsync(
        Completion draft,
        IReadOnlyList<CompletionValue>? values = null,
        IReadOnlyList<CompletionSegment>? segments = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var habit = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == draft.HabitNodeId, ct)
            ?? throw new HabitNodeNotFoundException(draft.HabitNodeId);
        if (habit.HabitType is null)
            throw new InvalidCompletionForHabitTypeException(draft.HabitNodeId, default, "node is a group, not a habit");
        ValidatePerType(habit.HabitType.Value, draft, values, segments);
        db.Completions.Add(draft);
        if (values is not null)
        {
            foreach (var v in values)
            {
                v.CompletionId = draft.Id;
                if (v.Id == Guid.Empty) v.Id = Guid.NewGuid();
                db.CompletionValues.Add(v);
            }
        }
        if (segments is not null)
        {
            foreach (var s in segments)
            {
                s.CompletionId = draft.Id;
                if (s.Id == Guid.Empty) s.Id = Guid.NewGuid();
                db.CompletionSegments.Add(s);
            }
        }
        await db.SaveChangesAsync(ct);
        LogRecorded(draft.Id, draft.HabitNodeId);
        eventBus.Publish(new CompletionRecordedPayload(draft.Id, draft.HabitNodeId, draft.CompletedAt));
        _ = clock;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Completion> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Completions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return;
        mutate(c);
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new CompletionUpdatedPayload(c.Id, c.HabitNodeId, c.CompletedAt));
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.Completions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return;
        await db.Completions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        eventBus.Publish(new CompletionDeletedPayload(c.Id, c.HabitNodeId, c.CompletedAt));
    }

    public async ValueTask<Completion?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Completions
            .Include(c => c.Values)
            .Include(c => c.Segments)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<Completion>> ListByHabitAsync(
        Guid habitNodeId,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int? take = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Completions
            .AsNoTracking()
            .Include(c => c.Values)
            .Where(c => c.HabitNodeId == habitNodeId);
        if (fromUtc is { } f)
        {
            var fMs = f.ToUnixTimeMilliseconds();
            q = q.Where(c => c.CompletedAtMs >= fMs);
        }
        if (toUtc is { } t)
        {
            var tMs = t.ToUnixTimeMilliseconds();
            q = q.Where(c => c.CompletedAtMs <= tMs);
        }
        var ordered = q.OrderByDescending(c => c.CompletedAtMs);
        return take is { } n
            ? await ordered.Take(n).ToListAsync(ct)
            : await ordered.ToListAsync(ct);
    }

    public async ValueTask<IReadOnlyList<HeatmapCell>> GetHeatmapAsync(
        Guid habitNodeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        var fromMs = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Completions.AsNoTracking()
            .Where(c => c.HabitNodeId == habitNodeId && c.CompletedAtMs >= fromMs && c.CompletedAtMs <= toMs)
            .Select(c => new { c.CompletedAt, c.IsRetroactive })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => DateOnly.FromDateTime(r.CompletedAt))
            .Select(g => new HeatmapCell(g.Key, g.Count(), g.Count(x => x.IsRetroactive)))
            .OrderBy(c => c.Date)
            .ToList();
    }

    public async ValueTask<IReadOnlyList<DailyValue>> GetDailySumAsync(
        Guid habitNodeId, Guid metricDefinitionId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var fromMs = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.CompletionValues.AsNoTracking()
            .Where(v => v.MetricDefinitionId == metricDefinitionId
                && v.Completion.HabitNodeId == habitNodeId
                && v.Completion.CompletedAtMs >= fromMs
                && v.Completion.CompletedAtMs <= toMs)
            .Select(v => new
            {
                v.Completion.CompletedAt,
                Numeric = (decimal?)v.ValueDecimal
                    ?? (decimal?)v.ValueInteger
                    ?? (decimal?)v.ValueSeconds
                    ?? 0m,
            })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => DateOnly.FromDateTime(r.CompletedAt))
            .Select(g => new DailyValue(g.Key, g.Sum(x => x.Numeric)))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async ValueTask<IReadOnlyList<DailyMagnitude>> GetDailyMagnitudeAsync(
        Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        var first = new DateOnly(year, month, 1);
        var next = first.AddMonths(1);
        var fromMs = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(next.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds(); // exclusive: [fromMs, toMs) covers the whole month

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.AsNoTracking()
            .Where(n => n.Id == habitNodeId)
            .Select(n => new { n.HabitType })
            .FirstOrDefaultAsync(ct);
        if (node is null) return [];

        Guid? primaryMetricId = await db.MetricDefinitions.AsNoTracking()
            .Where(m => m.HabitNodeId == habitNodeId)
            .OrderBy(m => m.SortOrder)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        var comps = await db.Completions.AsNoTracking()
            .Where(c => c.HabitNodeId == habitNodeId && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
            .Select(c => new
            {
                c.CompletedAt,
                c.CompletedAtMs,
                Values = c.Values.Select(v => new
                {
                    v.SeriesIndex,
                    v.MetricDefinitionId,
                    Numeric = (decimal?)v.ValueDecimal ?? (decimal?)v.ValueInteger ?? (decimal?)v.ValueSeconds ?? 0m,
                }).ToList(),
            })
            .ToListAsync(ct);

        var result = new List<DailyMagnitude>();
        foreach (var g in comps.GroupBy(c => DateOnly.FromDateTime(c.CompletedAt)))
        {
            decimal mag = node.HabitType switch
            {
                HabitType.Resistance => g.Sum(c => c.Values.Select(v => v.SeriesIndex).Distinct().Count()),
                HabitType.Counter => g.SelectMany(c => c.Values)
                    .Where(v => primaryMetricId == null || v.MetricDefinitionId == primaryMetricId.Value)
                    .Sum(v => v.Numeric),
                HabitType.State => g.OrderByDescending(c => c.CompletedAtMs)
                    .SelectMany(c => c.Values)
                    .Where(v => primaryMetricId == null || v.MetricDefinitionId == primaryMetricId.Value)
                    .Select(v => v.Numeric)
                    .DefaultIfEmpty(0m)
                    .First(),
                // Action, Journal, null → count completions
                _ => g.Count(),
            };
            if (mag > 0) result.Add(new DailyMagnitude(g.Key, mag));
        }
        return result.OrderBy(d => d.Date).ToList();
    }

    private static void ValidatePerType(
        HabitType type, Completion c,
        IReadOnlyList<CompletionValue>? values,
        IReadOnlyList<CompletionSegment>? segments)
    {
        _ = segments;
        switch (type)
        {
            case HabitType.Action:
                break;
            case HabitType.Counter:
            case HabitType.Resistance:
            case HabitType.State:
                if (values is null || values.Count == 0)
                    throw new InvalidCompletionForHabitTypeException(
                        c.HabitNodeId, type,
                        $"{type} completion requires at least one CompletionValue");
                break;
            case HabitType.Journal:
                if (string.IsNullOrWhiteSpace(c.Notes))
                    throw new InvalidCompletionForHabitTypeException(c.HabitNodeId, type, "Journal completion requires Notes");
                break;
        }
    }
}
