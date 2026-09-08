using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class StreakTimelineViewModel(IStreakAggregator aggregator, IClock clock) : ObservableObject
{
    [ObservableProperty] private DateOnly _rangeFrom;
    [ObservableProperty] private DateOnly _rangeTo;
    public ObservableCollection<StreakRun> Runs { get; } = [];

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = day.Today(clock);
        RangeTo = today;
        RangeFrom = today.AddDays(-89);
        var runs = await aggregator.GetHistoryAsync(node.Id, RangeFrom, RangeTo, ct).ConfigureAwait(true);
        Runs.Clear();
        foreach (var r in runs) Runs.Add(r);
    }
}
