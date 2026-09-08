using Microsoft.EntityFrameworkCore;
using Recordum.Core;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Data.Services.Queries;

public sealed class ExportQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ICompletionService completions,
    INoteService notes,
    IMetricDefinitionService metrics,
    IGoalService goals,
    IHabitConfigService configs,
    IIntensityRampResolver ramp,
    IStreakAggregator streaks,
    IAbstinenceService abstinence,
    IAttachmentService attachments,
    IReferenceDataQueryService refData,
    IResetTimeResolver resetTimes) : IExportQueryService
{
    private const int HeatmapCols = 53;
    private const int HeatmapRows = 7;

    public async ValueTask<ExportModel> BuildAsync(ExportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var generatedAt = clock.UtcNow;

        var habits = new List<ExportHabitModel>();
        if (request.Subject == ExportSubject.SingleHabit)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var node = request.HabitNodeId is { } id
                ? await db.HabitNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct).ConfigureAwait(false)
                : null;
            if (node is { NodeType: NodeType.Habit })
                habits.Add(await BuildHabitAsync(node, request, ct).ConfigureAwait(false));
        }
        else
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var nodes = await db.HabitNodes.AsNoTracking()
                .Where(n => n.NodeType == NodeType.Habit)
                .OrderBy(n => n.Name)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var node in nodes)
                habits.Add(await BuildHabitAsync(node, request, ct).ConfigureAwait(false));
        }

        var title = request.Subject == ExportSubject.SingleHabit
            ? habits.FirstOrDefault()?.Name ?? ""
            : "Recordum Library";
        return new ExportModel(request.Subject, title, request.From, request.To, generatedAt, habits);
    }

    private async Task<ExportHabitModel> BuildHabitAsync(HabitNode node, ExportRequest request, CancellationToken ct)
    {
        // Il confine e' quello dell'abitudine esportata, risolto qui dentro e non una volta per tutte:
        // un export di libreria copre abitudini con reset_time diversi, ed e' tutto il punto della cascata.
        var day = await resetTimes.GetDayBoundaryAsync(node.Id, ct).ConfigureAwait(false);
        var (fromUtc, toExclusive) = day.RangeOf(request.From, request.To);
        // ListByHabitAsync ha il limite superiore inclusivo: l'ultimo istante che appartiene ancora
        // alla finestra semiaperta e' un tick prima del suo estremo.
        var toUtc = toExclusive.AddTicks(-1);

        var isResistance = node.HabitType == HabitType.Resistance;
        List<ExportPeriodRow> periodRows = [];
        if (isResistance)
        {
            var periods = await abstinence.ListPeriodsAsync(node.Id, request.From, request.To, ct).ConfigureAwait(false);
            var now = clock.UtcNow;
            periodRows = periods
                .OrderByDescending(p => p.StartedAtMs)
                .Select(p =>
                {
                    var start = DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs);
                    DateTimeOffset? end = p.EndedAtMs is { } e ? DateTimeOffset.FromUnixTimeMilliseconds(e) : null;
                    return new ExportPeriodRow(start, end, (end ?? now) - start, end is null);
                })
                .ToList();
        }

        var metricList = await metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(false);
        var metricNames = metricList.ToDictionary(m => m.Id, m => m.Name);
        var units = await refData.ListUnitsAsync(ct).ConfigureAwait(false);
        var metricUnits = metricList.ToDictionary(
            m => m.Id,
            m => m.UnitOfMeasureId is { } uid ? units.FirstOrDefault(u => u.Id == uid)?.Code : null);

        var completionRows = (!isResistance && request.Layout.Completions)
            ? (await completions.ListByHabitAsync(node.Id, fromUtc, toUtc, take: null, ct).ConfigureAwait(false))
                .Select(c =>
                {
                    var primaryValue = c.Values.FirstOrDefault();
                    var declaredUnit = primaryValue is null ? null : metricUnits.GetValueOrDefault(primaryValue.MetricDefinitionId);
                    decimal? value = primaryValue is null
                        ? null
                        : primaryValue.ValueDecimal
                            ?? (primaryValue.ValueInteger.HasValue ? (decimal?)primaryValue.ValueInteger.Value : null)
                            ?? (primaryValue.ValueSeconds.HasValue
                                ? (Recordum.Core.Formatting.SecondsDisplay.ConvertToUnit(
                                       primaryValue.ValueSeconds.Value,
                                       declaredUnit)
                                   ?? primaryValue.ValueSeconds.Value)
                                : null);
                    // Name the unit the exported number is actually in. A duration with no time unit
                    // to convert into stays a second count, whatever the metric declares.
                    var valueUnit = primaryValue?.ValueSeconds.HasValue == true
                                    && !Recordum.Core.Formatting.SecondsDisplay.IsTimeUnit(declaredUnit)
                        ? "seconds"
                        : declaredUnit;
                    return new ExportCompletionRow(
                        new DateTimeOffset(DateTime.SpecifyKind(c.CompletedAt, DateTimeKind.Utc), TimeSpan.Zero),
                        value,
                        primaryValue is null ? null : metricNames.GetValueOrDefault(primaryValue.MetricDefinitionId),
                        c.IsRetroactive)
                    {
                        Unit = valueUnit,
                    };
                })
                .ToList()
            : [];

        var noteRows = new List<ExportNoteRow>();
        if (request.Layout.Notes)
        {
            foreach (var n in await notes.ListByHabitAsync(node.Id, ct).ConfigureAwait(false))
            {
                var noteAttachments = await attachments.ListByNoteAsync(n.Id, ct).ConfigureAwait(false);
                var attachmentRefs = noteAttachments
                    .Select(a => new ExportAttachmentRef(a.Filename, a.MimeType, attachments.ResolveRelativePath(a))
                    {
                        AbsolutePath = attachments.ResolveAbsolutePath(a),
                    })
                    .ToList();
                noteRows.Add(new ExportNoteRow(DateTimeOffset.FromUnixTimeMilliseconds(n.UpdatedAtMs), n.BodyMd)
                {
                    Attachments = attachmentRefs,
                });
            }
        }

        var summary = await BuildSummaryAsync(node, request, metricList, metricUnits, day, ct).ConfigureAwait(false);
        if (isResistance) summary = summary with { TotalCompletions = periodRows.Count };
        var heatmap = request.Layout.Heatmap
            ? await BuildHeatmapAsync(node, day, ct).ConfigureAwait(false)
            : new List<ExportHeatmapCell>();

        return new ExportHabitModel(node.Id, node.Name, node.HabitType?.ToString() ?? "", node.Color,
            summary, completionRows, noteRows, heatmap) { Periods = periodRows };
    }

    private async Task<ExportSummary> BuildSummaryAsync(
        HabitNode node,
        ExportRequest request,
        IReadOnlyList<MetricDefinition> metricList,
        IReadOnlyDictionary<Guid, string?> metricUnits,
        DayBoundary day,
        CancellationToken ct)
    {
        var runs = await streaks.GetHistoryAsync(node.Id, request.From, request.To, ct).ConfigureAwait(false);
        var current = runs.FirstOrDefault(r => r.IsCurrent)?.Length ?? 0;
        var longest = runs.Count == 0 ? 0 : runs.Max(r => r.Length);

        var (fromUtc, toExclusive) = day.RangeOf(request.From, request.To);
        var total = (await completions.ListByHabitAsync(node.Id, fromUtc, toExclusive.AddTicks(-1), take: null, ct).ConfigureAwait(false)).Count;

        var goal = await goals.GetActiveAsync(node.Id, ct).ConfigureAwait(false);
        decimal? target = goal?.TargetValue;
        decimal? actual = null;
        string? goalUnit = null;
        if (goal is not null)
        {
            if (goal.MetricDefinitionId is { } mid)
            {
                var raw = (await completions.GetDailySumAsync(node.Id, mid, request.From, request.To, ct).ConfigureAwait(false)).Sum(r => r.Sum);
                // A duration metric sums value_seconds, but the target was typed in the metric's unit —
                // report both in the same unit, or a 7.5-hour night reads as 27000 against a target of 10.
                var isDuration = metricList.FirstOrDefault(m => m.Id == mid)?.Type == MetricType.Seconds;
                var declaredUnit = metricUnits.GetValueOrDefault(mid);
                actual = Recordum.Core.Formatting.SecondsDisplay.ConvertTotal(raw, isDuration, declaredUnit);
                // Same rule as the completion rows: name the unit the pair is actually in, which for
                // a duration with no time unit to convert into is still seconds.
                goalUnit = isDuration && !Recordum.Core.Formatting.SecondsDisplay.IsTimeUnit(declaredUnit)
                    ? "seconds"
                    : declaredUnit;
            }
            else
            {
                // A goal with no metric counts completions: nothing to name.
                actual = total;
            }
        }
        return new ExportSummary(current, longest, total, actual, target) { GoalUnit = goalUnit };
    }

    private async Task<IReadOnlyList<ExportHeatmapCell>> BuildHeatmapAsync(HabitNode node, DayBoundary day, CancellationToken ct)
    {
        var today = day.Today(clock);
        var mondayOfTodayWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var anchor = mondayOfTodayWeek.AddDays(-(HeatmapCols - 1) * HeatmapRows);

        if (node.HabitType == HabitType.Resistance)
        {
            var periods = await abstinence.ListPeriodsAsync(node.Id, anchor, today, ct).ConfigureAwait(false);
            var clean = new HashSet<DateOnly>();
            foreach (var p in periods)
            {
                var s = day.DayOf(p.StartedAtMs);
                var isOpen = p.EndedAtMs is null;
                var e = p.EndedAtMs is { } end ? day.DayOf(end) : today;
                // Closed period: the relapse end-day is NOT a clean day (mirrors YearHeatmapViewModel.LoadAbstinenceAsync / Cronologia calendar).
                var lastClean = isOpen ? e : e.AddDays(-1);
                for (var d = s < anchor ? anchor : s; d <= lastClean && d <= today; d = d.AddDays(1)) clean.Add(d);
            }
            var resistanceCells = new List<ExportHeatmapCell>();
            for (int i = 0; i < HeatmapCols * HeatmapRows; i++)
            {
                var d = anchor.AddDays(i);
                if (d > today) continue;
                if (clean.Contains(d)) resistanceCells.Add(new ExportHeatmapCell(i / HeatmapRows, i % HeatmapRows, 4));
            }
            return resistanceCells;
        }

        Dictionary<DateOnly, decimal> values;
        // Resistance already returned above (line 160-183) — never reachable here.
        if (node.HabitType is HabitType.Counter or HabitType.State)
        {
            var primary = (await metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(false))
                .OrderBy(m => m.SortOrder).FirstOrDefault();
            values = primary is not null
                ? (await completions.GetDailySumAsync(node.Id, primary.Id, anchor, today, ct).ConfigureAwait(false)).ToDictionary(r => r.Date, r => r.Sum)
                : (await completions.GetHeatmapAsync(node.Id, anchor, today, ct).ConfigureAwait(false)).ToDictionary(c => c.Date, c => (decimal)c.Count);
        }
        else
        {
            values = (await completions.GetHeatmapAsync(node.Id, anchor, today, ct).ConfigureAwait(false)).ToDictionary(c => c.Date, c => (decimal)c.Count);
        }

        var config = await configs.GetAsync(node.Id, ct).ConfigureAwait(false);
        var goal = await goals.GetActiveAsync(node.Id, ct).ConfigureAwait(false);
        var rampSteps = ramp.Resolve(node, config, goal);

        var cells = new List<ExportHeatmapCell>(HeatmapCols * HeatmapRows);
        for (int i = 0; i < HeatmapCols * HeatmapRows; i++)
        {
            var d = anchor.AddDays(i);
            if (d > today) continue;
            var v = values.GetValueOrDefault(d, 0m);
            int step = v <= 0 ? 0 : v < rampSteps.Step1 ? 1 : v < rampSteps.Step2 ? 2 : v < rampSteps.Step3 ? 3 : 4;
            if (step > 0) cells.Add(new ExportHeatmapCell(i / HeatmapRows, i % HeatmapRows, step));
        }
        return cells;
    }
}
