using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class HourlyHistogramViewModel(
    IHourlyDistributionQueryService service,
    IClock clock) : ObservableObject
{
    [ObservableProperty] private int _maxCount;

    public int[] Buckets { get; private set; } = new int[24];
    public TrendRange CurrentRange { get; private set; } = TrendRange.Days30;
    public HabitNode? Node { get; private set; }

    private DayBoundary _day = DayBoundary.Utc;

    public Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        _day = day;
        return SetRangeAsync(CurrentRange, ct);
    }

    public async Task SetRangeAsync(TrendRange range, CancellationToken ct = default)
    {
        if (Node is null) return;
        CurrentRange = range;
        var today = _day.Today(clock);
        var from = range switch
        {
            TrendRange.Days30 => today.AddDays(-29),
            TrendRange.Days90 => today.AddDays(-89),
            TrendRange.Year1 => today.AddDays(-364),
            _ => today.AddDays(-29),
        };
        var raw = await service.GetHourlyAsync(Node.Id, from, today, ct).ConfigureAwait(true);
        Buckets = raw.ToArray();
        MaxCount = Buckets.Length == 0 ? 0 : Buckets.Max();
        OnPropertyChanged(nameof(Buckets));
    }
}
