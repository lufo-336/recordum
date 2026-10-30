#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI strings
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class GoalSectionViewModel(IGoalService goals) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Goal;

    public override bool HasAdvanced => true;

    // ponytail: niente unita' nel riassunto: il Goal non la porta (sta sulla metrica) e la sezione non la conosce.
    public override string Summary
    {
        get
        {
            if (TargetValue <= 0 && string.IsNullOrWhiteSpace(GoalName)) return Strings.Editor_Summary_NoGoal;
            var culture = CultureInfo.CurrentCulture;
            var period = PeriodType switch
            {
                GoalPeriodType.Rolling => string.Format(culture, Strings.Detail_GoalCard_Period_LastNDays, RollingDays ?? 0),
                GoalPeriodType.Fixed => PeriodStart is { } s && PeriodEnd is { } e
                    ? string.Create(culture, $"{s:d} – {e:d}") : Strings.Editor_GoalPeriod_Fixed,
                _ => Strings.Editor_GoalPeriod_Open,
            };
            var tail = string.Create(culture, $"{TargetValue:0.##} {period}");
            return string.IsNullOrWhiteSpace(GoalName) ? tail : $"{GoalName} · {tail}";
        }
    }

    private readonly IGoalService _goals = goals;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private string _goalName = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private decimal _targetValue;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private GoalPeriodType _periodType = GoalPeriodType.Open;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private DateOnly? _periodStart;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private DateOnly? _periodEnd;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private int? _rollingDays;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            var active = await _goals.GetActiveAsync(node.Id);
            GoalName = active?.Name ?? string.Empty;
            TargetValue = active?.TargetValue ?? 0m;
            PeriodType = active?.PeriodType ?? GoalPeriodType.Open;
            PeriodStart = active?.PeriodStart;
            PeriodEnd = active?.PeriodEnd;
            RollingDays = active?.RollingDays;
        }
        finally { _loading = false; }
    }

    // Ogni modifica scrive l'obiettivo intero dal suo stato in memoria: un Goal nuovo nasce con il periodo di default
    // dell'enum (Fixed, senza date: viola ck_goal_period_consistency) e un periodo rifiutato non deve lasciare DB e
    // schermata divergenti. Una combinazione non valida e' un errore del ViewModel, non un'eccezione del DB.
    private void Save()
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (TargetValue < 0)
        {
            ErrorMessage = Strings.Editor_Error_TargetNonNegative;
            Status = EditorStatus.Error;
            return;
        }
        if (PeriodType == GoalPeriodType.Rolling && RollingDays is null or <= 0)
        {
            ErrorMessage = Strings.Editor_Error_RollingDaysPositive;
            Status = EditorStatus.Error;
            return;
        }
        if (PeriodType == GoalPeriodType.Fixed && (PeriodStart is null || PeriodEnd is null))
        {
            ErrorMessage = Strings.Editor_Error_FixedDatesRequired;
            Status = EditorStatus.Error;
            return;
        }
        var (name, target, type, start, end, days) = (GoalName, TargetValue, PeriodType, PeriodStart, PeriodEnd, RollingDays);
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g =>
        {
            g.Name = name; g.TargetValue = target; g.PeriodType = type;
            g.PeriodStart = start; g.PeriodEnd = end; g.RollingDays = days;
        }).AsTask());
    }

    partial void OnGoalNameChanged(string value) => Save();
    partial void OnTargetValueChanged(decimal value) => Save();
    partial void OnPeriodTypeChanged(GoalPeriodType value) => Save();
    partial void OnPeriodStartChanged(DateOnly? value) => Save();
    partial void OnPeriodEndChanged(DateOnly? value) => Save();
    partial void OnRollingDaysChanged(int? value) => Save();
}
