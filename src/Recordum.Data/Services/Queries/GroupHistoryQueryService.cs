using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.Data.Services.Queries;

/// <summary>I giorni previsti e fatti delle figlie di un gruppo, e l'inizio di un'abitudine. Il giorno è
/// sempre quello della figlia (confini in batch, una volta); le registrazioni si leggono in una query sola.</summary>
public sealed class GroupHistoryQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IResetTimeResolver resetTimes,
    IScheduleEvaluator scheduler) : IGroupHistoryQueryService, IHabitStartQueryService
{
    private sealed record NodeRow(
        Guid Id, Guid? ParentId, NodeType NodeType, HabitType? HabitType, string Name, string Color,
        bool IsArchived, DateTime CreatedAt, ScheduleType? ScheduleType, string? DaysJson,
        int? WeeklyTarget, int? EveryXDays, string? MonthlyDaysJson);

    public async ValueTask<DateOnly> GetStartDayAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.HabitNodes.AsNoTracking().IgnoreQueryFilters()
            .Where(n => n.Id == habitNodeId).Select(n => new { n.CreatedAt, n.HabitType }).FirstAsync(ct)
            .ConfigureAwait(false);
        var boundary = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
        var ids = new[] { habitNodeId };
        var starts = await StartsAsync(db, ids,
            row.HabitType == HabitType.Resistance ? ids : [],
            new Dictionary<Guid, DayBoundary> { [habitNodeId] = boundary }, ct).ConfigureAwait(false);
        return starts.TryGetValue(habitNodeId, out var s) ? s : boundary.DayOf(row.CreatedAt);
    }

    public async ValueTask<IReadOnlyList<GroupChildHistory>> GetAsync(
        Guid groupId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        // Il filtro globale lascia fuori i cancellati; i figli di un cancellato non si raggiungono.
        var nodes = await db.HabitNodes.AsNoTracking()
            .Select(n => new NodeRow(n.Id, n.ParentId, n.NodeType, n.HabitType, n.Name, n.Color, n.IsArchived,
                n.CreatedAt,
                n.Config == null ? null : n.Config.ScheduleType, n.Config == null ? null : n.Config.ScheduleDaysJson,
                n.Config == null ? null : n.Config.ScheduleWeeklyTarget,
                n.Config == null ? null : n.Config.ScheduleEveryXDays,
                n.Config == null ? null : n.Config.ScheduleMonthlyDaysJson))
            .ToListAsync(ct).ConfigureAwait(false);
        var sortOrder = await db.HabitNodes.AsNoTracking().ToDictionaryAsync(n => n.Id, n => n.SortOrder, ct)
            .ConfigureAwait(false);
        var byParent = nodes.Where(n => n.ParentId is not null).ToLookup(n => n.ParentId!.Value);

        var children = new List<(NodeRow Node, int Depth)>();
        // Profondità: 0 = figlia diretta del gruppo, +1 per ogni sottogruppo attraversato.
        void WalkDepth(Guid parent, int depth)
        {
            foreach (var n in byParent[parent].Where(n => !n.IsArchived).OrderBy(n => sortOrder[n.Id]))
            {
                if (n.NodeType == NodeType.Group) WalkDepth(n.Id, depth + 1);
                else children.Add((n, depth));
            }
        }
        WalkDepth(groupId, 0);
        if (children.Count == 0) return [];

        var ids = children.Select(c => c.Node.Id).ToList();
        var resistanceIds = children.Where(c => c.Node.HabitType == HabitType.Resistance).Select(c => c.Node.Id).ToList();
        var boundaries = await resetTimes.GetDayBoundariesAsync(ids, ct).ConfigureAwait(false);
        var starts = await StartsAsync(db, ids, resistanceIds, boundaries, ct).ConfigureAwait(false);

        // Una query per le registrazioni di tutte le figlie nella finestra (unione delle finestre).
        var ranges = boundaries.Values.Select(b => b.RangeOf(from, to)).ToList();
        var fromMs = ranges.Min(r => r.From).ToUnixTimeMilliseconds();
        var toMs = ranges.Max(r => r.ToExclusive).ToUnixTimeMilliseconds();
        var plainIds = ids.Except(resistanceIds).ToList();
        var records = await db.Completions.AsNoTracking()
            .Where(c => plainIds.Contains(c.HabitNodeId) && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
            .Select(c => new { c.HabitNodeId, c.CompletedAtMs }).ToListAsync(ct).ConfigureAwait(false);
        var recordsBy = records.ToLookup(r => r.HabitNodeId);
        var periods = await db.AbstinencePeriods.AsNoTracking()
            .Where(p => resistanceIds.Contains(p.HabitNodeId)).ToListAsync(ct).ConfigureAwait(false);
        var periodsBy = periods.ToLookup(p => p.HabitNodeId);

        var result = new List<GroupChildHistory>(children.Count);
        foreach (var (n, depth) in children)
        {
            var boundary = boundaries[n.Id];
            var today = boundary.Today(clock);
            var creation = boundary.DayOf(n.CreatedAt);
            var isResistance = n.HabitType == HabitType.Resistance;
            var started = starts.TryGetValue(n.Id, out var s);
            var startsOn = started ? s : creation;
            var last = to < today ? to : today;
            var first = from > startsOn ? from : startsOn;

            var scheduled = ImmutableHashSet.CreateBuilder<DateOnly>();
            var done = ImmutableHashSet.CreateBuilder<DateOnly>();
            if (isResistance)
            {
                if (started)
                {
                    for (var d = first; d <= last; d = d.AddDays(1)) scheduled.Add(d);
                    var lapses = new HashSet<DateOnly>();
                    foreach (var p in periodsBy[n.Id])
                    {
                        // Periodo a durata zero (riparazione dell'unione): non è una ricaduta e non apre buchi.
                        if (p.EndedAtMs == p.StartedAtMs) continue;
                        var pFrom = boundary.DayOf(p.StartedAtMs);
                        var pTo = today;
                        if (p.EndedAtMs is { } end)
                        {
                            var lapse = boundary.DayOf(end);
                            lapses.Add(lapse);
                            pTo = lapse.AddDays(-1);
                        }
                        for (var d = pFrom > first ? pFrom : first; d <= pTo && d <= last; d = d.AddDays(1)) done.Add(d);
                    }
                    done.ExceptWith(lapses);
                }
            }
            else
            {
                var rules = (DateOnly day) => new ScheduledRules(
                    n.ScheduleType ?? ScheduleType.Daily, n.DaysJson, n.WeeklyTarget, n.EveryXDays,
                    n.MonthlyDaysJson, startsOn);
                for (var d = first; d <= last; d = d.AddDays(1))
                    if (scheduler.IsScheduled(d, rules(d))) scheduled.Add(d);
                foreach (var r in recordsBy[n.Id])
                {
                    var d = boundary.DayOf(r.CompletedAtMs);
                    if (d >= from && d <= last) done.Add(d);
                }
            }

            result.Add(new GroupChildHistory(n.Id, n.Name, n.Color, n.HabitType ?? HabitType.Action, depth, startsOn,
                scheduled.ToImmutable(), done.ToImmutable()));
        }
        return result;
    }

    /// <summary>Il giorno d'inizio per le abitudini che ne hanno uno: il più vecchio fra creazione e prima
    /// registrazione; per un'astinenza il primo periodo (mai iniziata = assente dal risultato).</summary>
    private static async Task<Dictionary<Guid, DateOnly>> StartsAsync(
        RecordumDbContext db, IReadOnlyCollection<Guid> ids, IReadOnlyCollection<Guid> resistanceIds,
        IReadOnlyDictionary<Guid, DayBoundary> boundaries, CancellationToken ct)
    {
        var result = new Dictionary<Guid, DateOnly>();
        var plain = ids.Except(resistanceIds).ToList();
        var creations = await db.HabitNodes.AsNoTracking().IgnoreQueryFilters()
            .Where(n => plain.Contains(n.Id)).Select(n => new { n.Id, n.CreatedAt }).ToListAsync(ct)
            .ConfigureAwait(false);
        var firstRecords = await db.Completions.AsNoTracking().Where(c => plain.Contains(c.HabitNodeId))
            .GroupBy(c => c.HabitNodeId).Select(g => new { Id = g.Key, Ms = g.Min(c => c.CompletedAtMs) })
            .ToDictionaryAsync(x => x.Id, x => x.Ms, ct).ConfigureAwait(false);
        foreach (var c in creations)
        {
            var b = boundaries[c.Id];
            var day = b.DayOf(c.CreatedAt);
            if (firstRecords.TryGetValue(c.Id, out var ms) && b.DayOf(ms) is var rec && rec < day) day = rec;
            result[c.Id] = day;
        }
        var firstPeriods = await db.AbstinencePeriods.AsNoTracking()
            .Where(p => resistanceIds.Contains(p.HabitNodeId) && (p.EndedAtMs == null || p.EndedAtMs != p.StartedAtMs))
            .GroupBy(p => p.HabitNodeId).Select(g => new { Id = g.Key, Ms = g.Min(p => p.StartedAtMs) })
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var p in firstPeriods) result[p.Id] = boundaries[p.Id].DayOf(p.Ms);
        return result;
    }
}
