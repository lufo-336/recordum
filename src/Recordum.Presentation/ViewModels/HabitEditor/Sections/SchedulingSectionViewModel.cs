using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class SchedulingSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Scheduling;

    // Ordered day keys matching ScheduleEvaluator's 3-letter convention.
    private static readonly (string Key, string Label)[] DayDefinitions =
    [
        ("Mon", "Mon"), ("Tue", "Tue"), ("Wed", "Wed"), ("Thu", "Thu"),
        ("Fri", "Fri"), ("Sat", "Sat"), ("Sun", "Sun")
    ];

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private ScheduleType _scheduleType = ScheduleType.Daily;
    [ObservableProperty] private string? _scheduleDaysJson;
    [ObservableProperty] private int? _scheduleWeeklyTarget;
    [ObservableProperty] private int? _scheduleEveryXDays;
    [ObservableProperty] private string? _scheduleMonthlyDaysJson;

    /// <summary>
    /// Seven day toggles (Mon..Sun) bound by the view when ScheduleType == SpecificDays.
    /// Each toggle's IsSelected change re-serialises ScheduleDaysJson and persists.
    /// </summary>
    public IReadOnlyList<DayToggleViewModel> DayOptions { get; } =
        DayDefinitions.Select(d => new DayToggleViewModel(d.Key, d.Label)).ToList();

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

            // Parse and apply day selections.
            var selected = ParseDays(cfg?.ScheduleDaysJson);
            foreach (var day in DayOptions)
            {
                day.IsSelectedChanged -= OnDayIsSelectedChanged;
                day.IsSelected = selected.Contains(day.DayKey);
                day.IsSelectedChanged += OnDayIsSelectedChanged;
            }
        }
        finally { _loading = false; }
        return Task.CompletedTask;
    }

    // Abstinence ignores scheduling — the streak is "time since last lapse" (spec 2026-06-14).
    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit != HabitType.Resistance;

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
            ErrorMessage = Strings.Editor_Error_WeeklyTargetPositive;
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
            ErrorMessage = Strings.Editor_Error_EveryXDaysPositive;
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

    // Called when any DayToggleViewModel.IsSelected changes (hooked during LoadAsync).
    private void OnDayIsSelectedChanged(object? sender, EventArgs e)
    {
        if (_loading || NodeId == Guid.Empty) return;
        var selected = DayOptions
            .Where(d => d.IsSelected)
            .Select(d => d.DayKey)
            .ToArray();
        var json = selected.Length > 0
            ? JsonSerializer.Serialize(selected)
            : null;
        _loading = true;
        try { ScheduleDaysJson = json; }
        finally { _loading = false; }
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.ScheduleDaysJson = json).AsTask());
    }

    private static HashSet<string> ParseDays(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json);
            return arr is null ? [] : new HashSet<string>(arr, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return []; }
    }
}
