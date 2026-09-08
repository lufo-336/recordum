using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>Counter "Oggi" content (spec §1): today's total (+ unit), the active goal's progress, and a
/// "+ Registra" affordance that asks the host to open/focus the Cronologia record module. Today's total
/// is the sum of the primary metric's values for the day (same semantics as the Home row's
/// TodayCounterTotal); a bare counter with no metrics falls back to the day magnitude (occurrence
/// count). The goal reuses <see cref="GoalProgressCardViewModel"/>.</summary>
public sealed partial class CounterTodayContent : ObservableObject, ITodayContent, ITodayContentNavigates
{
    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IClock _clock;
    private readonly GoalProgressCardViewModel _goalCard;

    [ObservableProperty] private decimal _todayTotal;
    /// <summary>What the view shows: <see cref="TodayTotal"/> pre-formatted, so a unitless duration
    /// can read "12:00" instead of the raw second count (W23-VF2).</summary>
    [ObservableProperty] private string? _todayTotalDisplay;
    [ObservableProperty] private string? _unitCode;
    [ObservableProperty] private bool _hasGoal;
    [ObservableProperty] private double _goalFraction;

    public event EventHandler<TodayContentAction>? ActionRequested;

    public CounterTodayContent(
        ICompletionService completions, IMetricDefinitionService metrics,
        IReferenceDataQueryService refData, IGoalService goals, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(refData);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(clock);
        _completions = completions;
        _metrics = metrics;
        _refData = refData;
        _clock = clock;
        _goalCard = new GoalProgressCardViewModel(goals, completions, metrics, refData, clock);
    }

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(day);
        var today = day.Today(_clock);

        var metrics = await _metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(true);
        var primary = metrics.OrderBy(m => m.SortOrder).FirstOrDefault();

        var isDuration = false;
        if (primary is not null)
        {
            // Sum of the primary metric's values today — same semantics as the Home row's TodayCounterTotal.
            var sums = await _completions.GetDailySumAsync(node.Id, primary.Id, today, today, ct).ConfigureAwait(true);
            var rawSum = sums.FirstOrDefault(s => s.Date == today)?.Sum ?? 0m;
            UnitCode = await ResolveUnitCodeAsync(primary, ct).ConfigureAwait(true);
            // A duration (Seconds) metric stores value_seconds; show the total in the metric's own unit
            // (90s on a "minutes" metric = 1.5), not the raw second count with a "minutes" label.
            isDuration = primary.Type == MetricType.Seconds;
            TodayTotal = Recordum.Core.Formatting.SecondsDisplay.ConvertTotal(rawSum, isDuration, UnitCode);
        }
        else
        {
            // Bare counter: occurrences are the total (each tap = 1).
            var mags = await _completions.GetDailyMagnitudeAsync(node.Id, today.Year, today.Month, ct).ConfigureAwait(true);
            TodayTotal = mags.FirstOrDefault(m => m.Date == today)?.Magnitude ?? 0m;
            UnitCode = null;
        }

        // A duration with no time unit is still raw seconds: render it as h:mm:ss so this total agrees
        // with the records list; every other case keeps the plain number the view always showed.
        TodayTotalDisplay = Formatting.CounterTotalDisplay.AsRawDuration(isDuration, UnitCode, TodayTotal)
            ?? TodayTotal.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

        await _goalCard.LoadAsync(node, ct).ConfigureAwait(true);
        HasGoal = !_goalCard.IsEmpty;
        GoalFraction = HasGoal ? Math.Clamp(_goalCard.PercentComplete, 0d, 1d) : 0d;
    }

    private async Task<string?> ResolveUnitCodeAsync(MetricDefinition primary, CancellationToken ct)
    {
        if (primary.UnitOfMeasureId is not { } uid) return null;
        var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
        return units.FirstOrDefault(u => u.Id == uid)?.Code;
    }

    [RelayCommand]
    private void Record() => ActionRequested?.Invoke(this, TodayContentAction.OpenRecordModule);
}
