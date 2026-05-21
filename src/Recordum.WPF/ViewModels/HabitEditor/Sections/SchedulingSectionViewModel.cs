using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class SchedulingSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private ScheduleType _scheduleType = ScheduleType.Daily;
    [ObservableProperty] private string? _scheduleDaysJson;
    [ObservableProperty] private int? _scheduleWeeklyTarget;
    [ObservableProperty] private int? _scheduleEveryXDays;
    [ObservableProperty] private string? _scheduleMonthlyDaysJson;

    public Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            ScheduleType = cfg?.ScheduleType ?? ScheduleType.Daily;
            ScheduleDaysJson = cfg?.ScheduleDaysJson;
            ScheduleWeeklyTarget = cfg?.ScheduleWeeklyTarget;
            ScheduleEveryXDays = cfg?.ScheduleEveryXDays;
            ScheduleMonthlyDaysJson = cfg?.ScheduleMonthlyDaysJson;
        }
        finally { _loading = false; }
        return Task.CompletedTask;
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;

    partial void OnScheduleTypeChanged(ScheduleType value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.ScheduleType = value).AsTask());
    }

    partial void OnScheduleDaysJsonChanged(string? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.ScheduleDaysJson = value).AsTask());
    }

    partial void OnScheduleWeeklyTargetChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (ScheduleType == ScheduleType.WeeklyX && (value is null or <= 0))
        {
            ErrorMessage = "Weekly target must be > 0";
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.ScheduleWeeklyTarget = value).AsTask());
    }

    partial void OnScheduleEveryXDaysChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (ScheduleType == ScheduleType.EveryXDays && (value is null or <= 0))
        {
            ErrorMessage = "Every X days must be > 0";
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.ScheduleEveryXDays = value).AsTask());
    }

    partial void OnScheduleMonthlyDaysJsonChanged(string? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.ScheduleMonthlyDaysJson = value).AsTask());
    }
}
