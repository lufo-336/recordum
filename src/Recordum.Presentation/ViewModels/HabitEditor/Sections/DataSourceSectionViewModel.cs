using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>
/// Placeholder for the external data-source integration (Health, Apple HealthKit, etc.).
/// Stored as a per-habit AppSetting until a schema decision lands (Milestone 3.6+).
/// </summary>
public sealed partial class DataSourceSectionViewModel(IAppSettingService settings) : SectionViewModelBase
{
    public override string Title => string.Empty;

    private readonly IAppSettingService _settings = settings;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private bool _isEnabled;

    public async Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        _ = cfg;
        _loading = true;
        try
        {
            NodeId = node.Id;
            var raw = await _settings.GetAsync(KeyFor(node.Id));
            IsEnabled = string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }
        finally { _loading = false; }
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit is HabitType.Action or HabitType.Counter or HabitType.Resistance or HabitType.State;

    partial void OnIsEnabledChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _settings.SetAsync(KeyFor(NodeId), value ? "true" : "false").AsTask());
    }

    internal static string KeyFor(Guid nodeId) => $"habit.{nodeId}.data_source";
}
