using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class StreakSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Streak;

    private const int MaxToleranceMinutes = 1440;

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private StreakMode _streakMode = StreakMode.Daily;
    [ObservableProperty] private StreakRecovery _streakRecovery = StreakRecovery.None;
    [ObservableProperty] private int? _streakToleranceMinutes;

    public Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            StreakMode = cfg?.StreakMode ?? StreakMode.Daily;
            StreakRecovery = cfg?.StreakRecovery ?? StreakRecovery.None;
            StreakToleranceMinutes = cfg?.StreakToleranceMinutes;
        }
        finally { _loading = false; }
        return Task.CompletedTask;
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;

    partial void OnStreakModeChanged(StreakMode value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.StreakMode = value).AsTask());
    }

    partial void OnStreakRecoveryChanged(StreakRecovery value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.StreakRecovery = value).AsTask());
    }

    partial void OnStreakToleranceMinutesChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (value is { } v && (v <= 0 || v > MaxToleranceMinutes))
        {
            ErrorMessage = Strings.Editor_Error_ToleranceRange;
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.StreakToleranceMinutes = value).AsTask());
    }
}
