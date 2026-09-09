using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>Action "Oggi" content (spec §1): a done-today toggle plus current streak, % of the last
/// 30 days done, and the usual completion hour. Records/undoes today's completion through
/// <see cref="ICompletionService"/> (the host's bus subscription reloads the columns); the streak reuses
/// the same <c>IsCurrent</c> run pattern as the Home row.</summary>
public sealed partial class ActionTodayContent : ObservableObject, ITodayContent
{
    private readonly ICompletionService _completions;
    private readonly IStreakAggregator _streaks;
    private readonly IHourlyDistributionQueryService _hourly;
    private readonly IClock _clock;
    private Guid _habitId;
    private Guid? _todayCompletionId;

    [ObservableProperty] private bool _isDoneToday;
    [ObservableProperty] private int _currentStreak;
    [ObservableProperty] private int _pct30;
    [ObservableProperty] private string? _usualHour;

    public bool HasUsualHour => !string.IsNullOrEmpty(UsualHour);

    public ActionTodayContent(
        ICompletionService completions, IStreakAggregator streaks,
        IHourlyDistributionQueryService hourly, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(streaks);
        ArgumentNullException.ThrowIfNull(hourly);
        ArgumentNullException.ThrowIfNull(clock);
        _completions = completions;
        _streaks = streaks;
        _hourly = hourly;
        _clock = clock;
    }

    private DayBoundary _day = DayBoundary.Utc;

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        _habitId = node.Id;
        _day = day;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    partial void OnUsualHourChanged(string? value) => OnPropertyChanged(nameof(HasUsualHour));

    private async Task ReloadAsync(CancellationToken ct)
    {
        var today = _day.Today(_clock);

        // Done today = any completion inside today's local-day window; keep the newest id to undo.
        // RangeOf è semiaperta ma il filtro del servizio è inclusivo al millisecondo: senza il -1ms
        // la finestra si mangia il primo istante del giorno dopo.
        var (dayStart, dayEndExclusive) = _day.RangeOf(today);
        var dayEnd = dayEndExclusive.AddMilliseconds(-1);
        var todays = await _completions.ListByHabitAsync(_habitId, dayStart, dayEnd, ct: ct).ConfigureAwait(true);
        _todayCompletionId = todays.OrderByDescending(c => c.CompletedAtMs).FirstOrDefault()?.Id;
        IsDoneToday = todays.Count > 0;

        // Current streak: the run flagged IsCurrent (same as HomeRowViewModel.LoadKeyFigureAsync).
        var runs = await _streaks.GetHistoryAsync(_habitId, today.AddYears(-1), today, ct).ConfigureAwait(true);
        CurrentStreak = runs.FirstOrDefault(r => r.IsCurrent)?.Length ?? 0;

        // % of the last 30 days with at least one completion (distinct local days ÷ 30).
        var (win30Start, _) = _day.RangeOf(today.AddDays(-29));
        var last30 = await _completions.ListByHabitAsync(_habitId, win30Start, dayEnd, ct: ct).ConfigureAwait(true);
        var distinctDays = last30
            .Select(c => _day.DayOf(c.CompletedAtMs))
            .Distinct().Count();
        Pct30 = (int)Math.Round(100.0 * distinctDays / 30.0, MidpointRounding.AwayFromZero);

        // Usual hour: the peak bucket over the last 90 days, or null when there is no data.
        var hours = await _hourly.GetHourlyAsync(_habitId, today.AddDays(-89), today, ct).ConfigureAwait(true);
        UsualHour = ResolveUsualHour(hours);
    }

    private static string? ResolveUsualHour(IReadOnlyList<int> hours)
    {
        if (hours is null || hours.Count == 0) return null;
        var peak = -1;
        var max = 0;
        for (var h = 0; h < hours.Count; h++)
            if (hours[h] > max) { max = hours[h]; peak = h; }
        return peak < 0 ? null : new TimeOnly(peak, 0).ToString("t", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task ToggleDoneAsync()
    {
        if (IsDoneToday)
        {
            if (_todayCompletionId is { } id) await _completions.DeleteAsync(id).ConfigureAwait(true);
        }
        else
        {
            await _completions.RecordAsync(new Completion
            {
                HabitNodeId = _habitId,
                CompletedAt = _clock.UtcNow.UtcDateTime,
                CompletedAtMs = _clock.UtcNowMs,
            }).ConfigureAwait(true);
        }
        await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
    }
}
