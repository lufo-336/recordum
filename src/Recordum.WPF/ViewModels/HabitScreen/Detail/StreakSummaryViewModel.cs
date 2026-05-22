using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class StreakSummaryViewModel(IStreakAggregator aggregator, IClock clock) : ObservableObject
{
    [ObservableProperty] private int _currentLength;
    [ObservableProperty] private int _longestLength;
    [ObservableProperty] private DateOnly? _longestStartedOn;
    [ObservableProperty] private int _runsLast90Days;
    [ObservableProperty] private bool _isEmpty;

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var allTime = await aggregator.GetHistoryAsync(node.Id, DateOnly.FromDateTime(node.CreatedAt), today, ct).ConfigureAwait(true);
        var last90 = await aggregator.GetHistoryAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);

        var longest = allTime.OrderByDescending(r => r.Length).FirstOrDefault();
        LongestLength = longest?.Length ?? 0;
        LongestStartedOn = longest?.Start;
        RunsLast90Days = last90.Count;

        var current = allTime.FirstOrDefault(r => r.End == today || r.End == today.AddDays(-1));
        CurrentLength = current?.Length ?? 0;
        IsEmpty = allTime.Count == 0;
    }
}
