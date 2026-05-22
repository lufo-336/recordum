using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HeatmapViewModel : ObservableObject
{
    private const int Days = 91;

    private readonly ICompletionService _completions;
    private readonly IClock _clock;
    private readonly IIntensityRampResolver _ramp;
    private readonly IGoalService _goals;
    private readonly IHabitConfigService _configs;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<HeatmapCellViewModel> Cells { get; } = [];

    public event EventHandler<DateOnly>? DateClicked;

    public HeatmapViewModel(
        ICompletionService completions,
        IClock clock,
        IIntensityRampResolver ramp,
        IGoalService goals,
        IHabitConfigService configs)
    {
        _completions = completions;
        _clock = clock;
        _ramp = ramp;
        _goals = goals;
        _configs = configs;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var from = today.AddDays(-(Days - 1));

        var rawTask = _completions.GetHeatmapAsync(habitNodeId, from, today, ct);
        var goalTask = _goals.GetActiveAsync(habitNodeId, ct);
        var configTask = _configs.GetAsync(habitNodeId, ct);
        var raw = await rawTask.ConfigureAwait(true);
        var goal = await goalTask.ConfigureAwait(true);
        var config = await configTask.ConfigureAwait(true);

        var ramp = _ramp.Resolve(new HabitNode { Id = habitNodeId }, config, goal);
        var byDate = raw.ToDictionary(c => c.Date, c => c.Count);
        Cells.Clear();
        for (int i = 0; i < Days; i++)
        {
            var d = from.AddDays(i);
            var count = byDate.GetValueOrDefault(d, 0);
            int? step = HasGoalRamp(ramp) ? StepFor(count, ramp) : null;
            Cells.Add(new HeatmapCellViewModel(d, count, step));
        }
    }

    private static bool HasGoalRamp(IntensityRamp ramp) => ramp != IntensityRamp.Fallback;

    private static int StepFor(decimal v, IntensityRamp r) =>
        v <= 0 ? 0 : v < r.Step1 ? 1 : v < r.Step2 ? 2 : v < r.Step3 ? 3 : 4;

    [RelayCommand]
    private void ClickCell(HeatmapCellViewModel? cell)
    {
        if (cell is null) return;
        DateClicked?.Invoke(this, cell.Date);
    }
}
