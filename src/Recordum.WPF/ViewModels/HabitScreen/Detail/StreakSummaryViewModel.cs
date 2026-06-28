using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Formatting;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class StreakSummaryViewModel(
    IStreakAggregator aggregator, IAbstinenceService abstinence, IClock clock) : ObservableObject
{
    [ObservableProperty] private int _currentLength;
    [ObservableProperty] private int _longestLength;
    [ObservableProperty] private DateOnly? _longestStartedOn;
    [ObservableProperty] private int _runsLast90Days;
    [ObservableProperty] private bool _isEmpty;

    /// <summary>The big "current streak" number, formatted for display. Resistance habits render
    /// the elapsed time since the last lapse ("2a 41g"); other types show the plain day count.</summary>
    [ObservableProperty] private string _currentDisplay = "0";

    /// <summary>Caption for the third stat: "Lapses (90d)" for Resistance, "Runs (90d)" otherwise.</summary>
    [ObservableProperty] private string _runsCaption = Strings.Detail_Streak_Runs90;

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var allTime = await aggregator.GetHistoryAsync(node.Id, DateOnly.FromDateTime(node.CreatedAt), today, ct).ConfigureAwait(true);

        var longest = allTime.OrderByDescending(r => r.Length).FirstOrDefault();
        LongestLength = longest?.Length ?? 0;
        LongestStartedOn = longest?.Start;

        // Only the open run (BreakReason == null) counts as current: a run ending yesterday that a
        // same-day lapse already closed must not keep feeding a stale CurrentLength (CR12).
        var current = allTime.FirstOrDefault(
            r => (r.End == today || r.End == today.AddDays(-1)) && r.BreakReason is null);
        CurrentLength = current?.Length ?? 0;
        IsEmpty = allTime.Count == 0;

        if (node.HabitType == HabitType.Resistance)
        {
            RunsCaption = Strings.Detail_Streak_Lapses90;
            var state = await abstinence.GetStateAsync(node.Id, ct).ConfigureAwait(true);
            CurrentDisplay = state.Status == AbstinenceStatus.Abstaining && state.CurrentStartedAtMs is { } sMs
                ? DurationFormat.FormatElapsed(clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(sMs))
                : Strings.Abstinence_NotStarted;

            var periods90 = await abstinence.ListPeriodsAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);
            RunsLast90Days = periods90.Count(p => p.EndedAtMs is { } e
                && DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(e).UtcDateTime) >= today.AddDays(-89));
        }
        else
        {
            RunsCaption = Strings.Detail_Streak_Runs90;
            var last90 = await aggregator.GetHistoryAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);
            RunsLast90Days = last90.Count;
            CurrentDisplay = CurrentLength.ToString(CultureInfo.InvariantCulture);
        }
    }
}
