using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public enum TrendRange
{
    Days30,
    Days90,
    Year1,
}

public enum TrendChartKind
{
    Line,
    Bar,
}

public sealed partial class TrendChartViewModel(ITrendQueryService trend, IClock clock) : ObservableObject
{
    [ObservableProperty] private TrendRange _selectedRange = TrendRange.Days30;
    [ObservableProperty] private TrendChartKind _chartKind;
    [ObservableProperty] private decimal _maxValue;

    public TrendGranularity CurrentGranularity { get; private set; } = TrendGranularity.Daily;
    public ObservableCollection<TrendPoint> Series { get; } = [];
    public HabitNode? Node { get; private set; }

    public event EventHandler<TrendRange>? RangeChanged;

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        ChartKind = node.HabitType is HabitType.Counter or HabitType.Resistance or HabitType.State
            ? TrendChartKind.Line
            : TrendChartKind.Bar;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task SetRangeAsync(TrendRange range, CancellationToken ct = default)
    {
        SelectedRange = range;
        await ReloadAsync(ct).ConfigureAwait(true);
        RangeChanged?.Invoke(this, range);
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        if (Node is null) return;
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var (from, gran) = SelectedRange switch
        {
            TrendRange.Days30 => (today.AddDays(-29), TrendGranularity.Daily),
            TrendRange.Days90 => (today.AddDays(-89), TrendGranularity.Daily),
            TrendRange.Year1 => (today.AddDays(-364), TrendGranularity.Weekly),
            _ => (today.AddDays(-29), TrendGranularity.Daily),
        };
        CurrentGranularity = gran;
        var points = await trend.GetTrendAsync(Node.Id, from, today, gran, ct).ConfigureAwait(true);
        Series.Clear();
        foreach (var p in points) Series.Add(p);
        MaxValue = points.Count == 0 ? 0 : points.Max(p => p.Value);
    }
}
