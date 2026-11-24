using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.Detail;

public sealed partial class YearHeatmapViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IIntensityRampResolver ramp,
    IAbstinenceService abstinence,
    IClock clock) : ObservableObject
{
    private const int Cols = 53;
    private const int Rows = 7;

    public ObservableCollection<HeatmapCellViewModel> Cells { get; } = [];

    public async Task LoadAsync(HabitNode node, HabitConfig? config, Goal? activeGoal, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = day.Today(clock);
        var mondayOfTodayWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var anchor = mondayOfTodayWeek.AddDays(-(Cols - 1) * Rows);

        // Resistance has no completions: colour the heatmap from abstinence periods instead. Clean days
        // (inside a period; the closed relapse end-day is not clean) fill full habit colour; relapses and
        // gaps stay blank, so the year reads as habit-coloured clean runs broken by relapses.
        if (node.HabitType == HabitType.Resistance)
        {
            await LoadAbstinenceAsync(node, anchor, today, day, ct).ConfigureAwait(true);
            return;
        }

        Dictionary<DateOnly, decimal> values;
        if (node.HabitType is HabitType.Counter or HabitType.Resistance or HabitType.State)
        {
            var primary = (await metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(true))
                .OrderBy(m => m.SortOrder).FirstOrDefault();
            if (primary is not null)
            {
                var rows = await completions.GetDailySumAsync(node.Id, primary.Id, anchor, today, ct).ConfigureAwait(true);
                values = rows.ToDictionary(r => r.Date, r => r.Sum);
            }
            else
            {
                values = (await completions.GetHeatmapAsync(node.Id, anchor, today, ct).ConfigureAwait(true))
                    .ToDictionary(c => c.Date, c => (decimal)c.Count);
            }
        }
        else
        {
            values = (await completions.GetHeatmapAsync(node.Id, anchor, today, ct).ConfigureAwait(true))
                .ToDictionary(c => c.Date, c => (decimal)c.Count);
        }

        var rampSteps = ramp.Resolve(node, config, activeGoal);
        Cells.Clear();
        var totalCells = Cols * Rows;
        for (int i = 0; i < totalCells; i++)
        {
            var d = anchor.AddDays(i);
            var v = values.GetValueOrDefault(d, 0m);
            var outOfRange = d > today;
            int step = outOfRange ? 0 : StepFor(v, rampSteps);
            Cells.Add(new HeatmapCellViewModel(d, (int)v, step, outOfRange));
        }
    }

    private static int StepFor(decimal v, IntensityRamp r) =>
        v <= 0 ? 0 : v < r.Step1 ? 1 : v < r.Step2 ? 2 : v < r.Step3 ? 3 : 4;

    private async Task LoadAbstinenceAsync(HabitNode node, DateOnly anchor, DateOnly today, DayBoundary day, CancellationToken ct)
    {
        var periods = await abstinence.ListPeriodsAsync(node.Id, anchor, today, ct).ConfigureAwait(true);
        var clean = new HashSet<DateOnly>();
        foreach (var p in periods)
        {
            if (p.EndedAtMs == p.StartedAtMs) continue;
            var start = day.DayOf(p.StartedAtMs);
            var isOpen = p.EndedAtMs is null;
            var end = isOpen
                ? today
                : day.DayOf(p.EndedAtMs!.Value);
            // Closed period: the relapse end-day is NOT a clean day (mirrors the Cronologia calendar).
            var lastClean = isOpen ? end : end.AddDays(-1);
            for (var d = start < anchor ? anchor : start; d <= lastClean && d <= today; d = d.AddDays(1))
                clean.Add(d);
        }

        Cells.Clear();
        var totalCells = Cols * Rows;
        for (int i = 0; i < totalCells; i++)
        {
            var d = anchor.AddDays(i);
            var outOfRange = d > today;
            var isClean = !outOfRange && clean.Contains(d);
            Cells.Add(new HeatmapCellViewModel(d, isClean ? 1 : 0, isClean ? 4 : 0, outOfRange));
        }
    }
}
