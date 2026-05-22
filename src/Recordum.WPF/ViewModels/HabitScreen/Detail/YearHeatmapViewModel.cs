using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class YearHeatmapViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IIntensityRampResolver ramp,
    IClock clock) : ObservableObject
{
    private const int Cols = 53;
    private const int Rows = 7;

    public ObservableCollection<HeatmapCellViewModel> Cells { get; } = [];

    public async Task LoadAsync(HabitNode node, HabitConfig? config, Goal? activeGoal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var mondayOfTodayWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var anchor = mondayOfTodayWeek.AddDays(-(Cols - 1) * Rows);

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
}
