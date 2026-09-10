using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.WPF.Resources;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class StreakSummaryViewModel : ObservableObject, IDisposable
{
    private readonly IStreakAggregator _aggregator;
    private readonly IAbstinenceService _abstinence;
    private readonly IClock _clock;
    private readonly LiveElapsedClock _liveClock;

    public StreakSummaryViewModel(IStreakAggregator aggregator, IAbstinenceService abstinence, IClock clock)
    {
        _aggregator = aggregator;
        _abstinence = abstinence;
        _clock = clock;
        _liveClock = new LiveElapsedClock(clock);
        // Forward each 1s tick to the bound CurrentDisplay so the Detail streak number stays live.
        _liveClock.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LiveElapsedClock.Display) && _liveClock.Display.Length > 0)
                CurrentDisplay = _liveClock.Display;
        };
    }

    [ObservableProperty] private int _currentLength;
    [ObservableProperty] private int _longestLength;
    [ObservableProperty] private DateOnly? _longestStartedOn;
    [ObservableProperty] private int _runsLast90Days;
    [ObservableProperty] private bool _isEmpty;

    /// <summary>The big "current streak" number, formatted for display. Resistance habits render
    /// the live elapsed time since the last lapse (e.g. "2g 6:14:09"); other types show the plain day count.</summary>
    [ObservableProperty] private string _currentDisplay = "0";

    /// <summary>Caption for the third stat: "Lapses (90d)" for Resistance, "Runs (90d)" otherwise.</summary>
    [ObservableProperty] private string _runsCaption = Strings.Detail_Streak_Runs90;

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = day.Today(_clock);
        var allTime = await _aggregator.GetHistoryAsync(node.Id, day.DayOf(node.CreatedAt), today, ct).ConfigureAwait(true);

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
            var state = await _abstinence.GetStateAsync(node.Id, ct).ConfigureAwait(true);
            if (state.Status == AbstinenceStatus.Abstaining && state.CurrentStartedAtMs is { } sMs)
            {
                _liveClock.Start(sMs);          // computes display now + ticks under a running app
                CurrentDisplay = _liveClock.Display;
            }
            else
            {
                _liveClock.Stop();
                CurrentDisplay = Strings.Abstinence_NotStarted;
            }

            var periods90 = await _abstinence.ListPeriodsAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);
            RunsLast90Days = periods90.Count(p => p.EndedAtMs is { } e
                && day.DayOf(e) >= today.AddDays(-89));
        }
        else
        {
            _liveClock.Stop(); // a reused VM switching off Resistance must not keep clobbering CurrentDisplay
            RunsCaption = Strings.Detail_Streak_Runs90;
            var last90 = await _aggregator.GetHistoryAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);
            RunsLast90Days = last90.Count;
            CurrentDisplay = CurrentLength.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Stops the live clock (and detaches its timer) so the Detail teardown leaks nothing.</summary>
    public void Dispose() => _liveClock.Dispose();
}
