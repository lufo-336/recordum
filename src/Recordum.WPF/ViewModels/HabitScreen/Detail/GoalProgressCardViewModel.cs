using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Formatting;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class GoalProgressCardViewModel(
    IGoalService goals,
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IReferenceDataQueryService refData,
    IClock clock) : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _actual;
    [ObservableProperty] private decimal _target;
    [ObservableProperty] private string _unitLabel = string.Empty;
    [ObservableProperty] private double _percentComplete;
    [ObservableProperty] private string _periodLabel = string.Empty;
    [ObservableProperty] private int? _daysLeft;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyMessage = string.Empty;

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var goal = await goals.GetActiveAsync(node.Id, ct).ConfigureAwait(true);
        if (goal is null)
        {
            IsEmpty = true;
            EmptyMessage = $"{Strings.Detail_GoalCard_NoGoal_Title} — {Strings.Detail_GoalCard_NoGoal_Hint}";
            Name = string.Empty;
            Actual = 0m;
            Target = 0m;
            PercentComplete = 0;
            PeriodLabel = string.Empty;
            DaysLeft = null;
            IsDone = false;
            return;
        }

        IsEmpty = false;
        Name = goal.Name;
        Target = goal.TargetValue;
        var (from, to) = ResolveRange(goal, node, day, clock);
        DaysLeft = ResolveDaysLeft(goal, day, clock);
        PeriodLabel = ResolveLabel(goal);

        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var list = await completions.ListByHabitAsync(node.Id, fromUtc, toUtc, take: null, ct).ConfigureAwait(true);

        if (goal.MetricDefinitionId is { } metricId)
        {
            var raw = list.SelectMany(c => c.Values)
                          .Where(v => v.MetricDefinitionId == metricId)
                          .Sum(v => v.ValueDecimal ?? (decimal?)v.ValueInteger ?? (decimal?)v.ValueSeconds ?? 0m);
            // A duration metric stores value_seconds, but the target was typed in the metric's unit —
            // compare like with like, or a 2-minute actual reads as 120 against a target of 4.
            var metric = (await metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(true))
                .FirstOrDefault(m => m.Id == metricId);
            Actual = SecondsDisplay.ConvertTotal(
                raw,
                metric?.Type == MetricType.Seconds,
                await ResolveUnitCodeAsync(metric, ct).ConfigureAwait(true));
            UnitLabel = string.Empty;
        }
        else
        {
            Actual = list.Count;
            UnitLabel = "completions";
        }

        PercentComplete = Target == 0 ? 0 : (double)(Actual / Target);
        IsDone = Actual >= Target && Target > 0;
    }

    private async Task<string?> ResolveUnitCodeAsync(MetricDefinition? metric, CancellationToken ct)
    {
        if (metric?.UnitOfMeasureId is not { } uid) return null;
        var units = await refData.ListUnitsAsync(ct).ConfigureAwait(true);
        return units.FirstOrDefault(u => u.Id == uid)?.Code;
    }

    private static (DateOnly from, DateOnly to) ResolveRange(Goal goal, HabitNode node, DayBoundary day, IClock clock)
    {
        var today = day.Today(clock);
        return goal.PeriodType switch
        {
            GoalPeriodType.Fixed => (goal.PeriodStart ?? today, (goal.PeriodEnd ?? today) < today ? goal.PeriodEnd!.Value : today),
            GoalPeriodType.Rolling => (today.AddDays(-((goal.RollingDays ?? 1) - 1)), today),
            _ => (day.DayOf(node.CreatedAt), today),
        };
    }

    private static int? ResolveDaysLeft(Goal goal, DayBoundary day, IClock clock)
    {
        if (goal.PeriodType != GoalPeriodType.Fixed || goal.PeriodEnd is null) return null;
        var today = day.Today(clock);
        var d = goal.PeriodEnd.Value.DayNumber - today.DayNumber;
        return d > 0 ? d : null;
    }

    private static string ResolveLabel(Goal goal) => goal.PeriodType switch
    {
        GoalPeriodType.Fixed => goal.PeriodStart is { } s && goal.PeriodEnd is { } e
            ? $"{s:MMM d} – {e:MMM d}"
            : Strings.Detail_GoalCard_Period_AllTime,
        GoalPeriodType.Rolling => goal.RollingDays == 30
            ? Strings.Detail_GoalCard_Period_Last30Days
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
            : string.Format(CultureInfo.CurrentCulture, Strings.Detail_GoalCard_Period_LastNDays, goal.RollingDays ?? 0),
#pragma warning restore CA1863
        _ => Strings.Detail_GoalCard_Period_AllTime,
    };
}
