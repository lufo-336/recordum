using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class GoalSectionViewModel(IGoalService goals) : SectionViewModelBase
{
    private readonly IGoalService _goals = goals;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private string _goalName = string.Empty;
    [ObservableProperty] private decimal _targetValue;
    [ObservableProperty] private GoalPeriodType _periodType = GoalPeriodType.Open;
    [ObservableProperty] private DateOnly? _periodStart;
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private int? _rollingDays;

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

    partial void OnGoalNameChanged(string value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g => g.Name = value).AsTask());
    }

    partial void OnTargetValueChanged(decimal value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (value < 0)
        {
            ErrorMessage = Strings.Editor_Error_TargetNonNegative;
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g => g.TargetValue = value).AsTask());
    }

    partial void OnPeriodTypeChanged(GoalPeriodType value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g => g.PeriodType = value).AsTask());
    }

    partial void OnPeriodStartChanged(DateOnly? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g => g.PeriodStart = value).AsTask());
    }

    partial void OnPeriodEndChanged(DateOnly? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g => g.PeriodEnd = value).AsTask());
    }

    partial void OnRollingDaysChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (PeriodType == GoalPeriodType.Rolling && (value is null or <= 0))
        {
            ErrorMessage = Strings.Editor_Error_RollingDaysPositive;
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _goals.UpsertActiveAsync(NodeId, g => g.RollingDays = value).AsTask());
    }
}
