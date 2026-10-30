using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class TimerSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Timer;

    public override bool HasAdvanced => true;

    public override string Summary
    {
        get
        {
            if (!HasIntegratedTimer) return Strings.Editor_Summary_Off;
            var mode = TimerMode switch
            {
                Recordum.Core.Enums.TimerMode.Countdown => Strings.Editor_TimerMode_Countdown,
                Recordum.Core.Enums.TimerMode.Primary => Strings.Editor_TimerMode_Primary,
                Recordum.Core.Enums.TimerMode.Session => Strings.Editor_TimerMode_Session,
                _ => Strings.Editor_TimerMode_Stopwatch,
            };
            return TimerTargetSeconds is int secs and > 0 ? $"{mode} · {Recordum.Presentation.Formatting.DurationFormat.Format(secs)}" : mode;
        }
    }

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private bool _hasIntegratedTimer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private TimerMode? _timerMode;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private int? _timerTargetSeconds;
    [ObservableProperty] private bool _timerAutoPrompt;
    [ObservableProperty] private int? _restTimerSeconds;
    [ObservableProperty] private bool _sessionBaseTimer;
    [ObservableProperty] private int? _sessionCountdownSeconds;

    public ObservableCollection<SegmentRowViewModel> Segments { get; } = new();

    public async Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            HasIntegratedTimer = cfg?.HasIntegratedTimer ?? false;
            TimerMode = cfg?.TimerMode;
            TimerTargetSeconds = cfg?.TimerTargetSeconds;
            TimerAutoPrompt = cfg?.TimerAutoPrompt ?? false;
            RestTimerSeconds = cfg?.RestTimerSeconds;
            SessionBaseTimer = cfg?.SessionBaseTimer ?? false;
            SessionCountdownSeconds = cfg?.SessionCountdownSeconds;

            Segments.Clear();
            foreach (var s in await _configs.ListSegmentsAsync(node.Id))
                Segments.Add(new SegmentRowViewModel(s, _configs));
        }
        finally { _loading = false; }
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit is HabitType.Counter or HabitType.Resistance;

    partial void OnHasIntegratedTimerChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.HasIntegratedTimer = value).AsTask());
    }

    partial void OnTimerModeChanged(TimerMode? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (value == Recordum.Core.Enums.TimerMode.Session && Segments.Count == 0)
        {
            ErrorMessage = Strings.Editor_Error_SessionRequiresSegment;
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c =>
        {
            c.TimerMode = value;
            // Session implies SessionBaseTimer per data invariant (HabitConfigService.ValidateCrossFields).
            if (value == Recordum.Core.Enums.TimerMode.Session) c.SessionBaseTimer = true;
        }).AsTask());
    }

    partial void OnTimerTargetSecondsChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.TimerTargetSeconds = value).AsTask());
    }

    partial void OnTimerAutoPromptChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.TimerAutoPrompt = value).AsTask());
    }

    partial void OnRestTimerSecondsChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.RestTimerSeconds = value).AsTask());
    }

    partial void OnSessionBaseTimerChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.SessionBaseTimer = value).AsTask());
    }

    partial void OnSessionCountdownSecondsChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.SessionCountdownSeconds = value).AsTask());
    }

    [RelayCommand]
    private async Task AddSegmentAsync()
    {
        if (NodeId == Guid.Empty) return;
        var seg = new SegmentDefinition
        {
            Id = Guid.NewGuid(),
            HabitNodeId = NodeId,
            Name = Strings.Editor_Default_NewSegment,
            SortOrder = Segments.Count,
        };
        await ApplyAsync(() => _configs.AddSegmentAsync(NodeId, seg).AsTask());
        Segments.Add(new SegmentRowViewModel(seg, _configs));
    }

    [RelayCommand]
    private async Task RemoveSegmentAsync(SegmentRowViewModel row)
    {
        if (row is null) return;
        await ApplyAsync(() => _configs.RemoveSegmentAsync(row.Id).AsTask());
        Segments.Remove(row);
    }
}

public sealed partial class SegmentRowViewModel(SegmentDefinition seg, IHabitConfigService configs) : ObservableObject
{
    private readonly IHabitConfigService _configs = configs;
    public Guid Id { get; } = seg.Id;

    [ObservableProperty] private string _name = seg.Name;
    [ObservableProperty] private int _sortOrder = seg.SortOrder;
    [ObservableProperty] private bool _isRest = seg.IsRest;

    partial void OnNameChanged(string value) => _ = _configs.UpdateSegmentAsync(Id, s => s.Name = value).AsTask();
    partial void OnSortOrderChanged(int value) => _ = _configs.UpdateSegmentAsync(Id, s => s.SortOrder = value).AsTask();
    partial void OnIsRestChanged(bool value) => _ = _configs.UpdateSegmentAsync(Id, s => s.IsRest = value).AsTask();
}
