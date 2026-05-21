using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitEditor.Sections;

namespace Recordum.WPF.ViewModels.HabitEditor;

/// <summary>
/// Right-anchored editor pane host. Owns L1 header fields (Name/Description/Color/Icon)
/// and the 11 section view-models. Persistence: per-field autosave on property change.
/// </summary>
public sealed partial class HabitEditorViewModel : ObservableObject
{
    private static readonly Regex HexColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    private readonly IHabitNodeService _nodes;
    private readonly IHabitConfigService _configs;
    private bool _loading;

    [ObservableProperty] private HabitNode? _node;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string _color = "#808080";
    [ObservableProperty] private string? _icon;
    [ObservableProperty] private NodeType _selectedNodeType = NodeType.Habit;
    [ObservableProperty] private HabitType? _selectedHabitType = HabitType.Action;

    [ObservableProperty] private EditorStatus _headerStatus = EditorStatus.Idle;
    [ObservableProperty] private string? _headerError;

    public SchedulingSectionViewModel Scheduling { get; }
    public StreakSectionViewModel Streak { get; }
    public TimerSectionViewModel Timer { get; }
    public MetricsSectionViewModel Metrics { get; }
    public ScalesSectionViewModel Scales { get; }
    public GoalSectionViewModel Goal { get; }
    public NotificationsSectionViewModel Notifications { get; }
    public TagsSectionViewModel Tags { get; }
    public AiSharingSectionViewModel AiSharing { get; }
    public DataSourceSectionViewModel DataSource { get; }
    public ResetTimeSectionViewModel ResetTime { get; }

    public bool HasNode => Node is not null;

    public HabitEditorViewModel(
        IHabitNodeService nodes,
        IHabitConfigService configs,
        IGoalService goals,
        IMetricDefinitionService metrics,
        INotificationService notifications,
        ITagService tags,
        IAppSettingService appSettings)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(configs);
        _nodes = nodes;
        _configs = configs;

        Scheduling = new SchedulingSectionViewModel(configs);
        Streak = new StreakSectionViewModel(configs);
        Timer = new TimerSectionViewModel(configs);
        Metrics = new MetricsSectionViewModel(metrics);
        Scales = new ScalesSectionViewModel(metrics);
        Goal = new GoalSectionViewModel(goals);
        Notifications = new NotificationsSectionViewModel(notifications);
        Tags = new TagsSectionViewModel(tags);
        AiSharing = new AiSharingSectionViewModel(nodes);
        DataSource = new DataSourceSectionViewModel(appSettings);
        ResetTime = new ResetTimeSectionViewModel(nodes);
    }

    public async Task LoadAsync(Guid? habitNodeId, CancellationToken ct = default)
    {
        _loading = true;
        try
        {
            if (habitNodeId is null)
            {
                Node = null;
                OnPropertyChanged(nameof(HasNode));
                return;
            }

            var node = await _nodes.GetAsync(habitNodeId.Value, ct);
            Node = node;
            if (node is null)
            {
                OnPropertyChanged(nameof(HasNode));
                return;
            }

            Name = node.Name;
            Description = node.Description;
            Color = node.Color;
            Icon = node.Icon;
            SelectedNodeType = node.NodeType;
            SelectedHabitType = node.HabitType;

            var cfg = await _configs.GetAsync(node.Id, ct);
            await Scheduling.LoadAsync(node, cfg);
            await Streak.LoadAsync(node, cfg);
            await Timer.LoadAsync(node, cfg);
            await Metrics.LoadAsync(node);
            await Scales.LoadAsync(node);
            await Goal.LoadAsync(node);
            await Notifications.LoadAsync(node);
            await Tags.LoadAsync(node);
            await AiSharing.LoadAsync(node);
            await DataSource.LoadAsync(node, cfg);
            await ResetTime.LoadAsync(node);

            HeaderStatus = EditorStatus.Idle;
            HeaderError = null;
            OnPropertyChanged(nameof(HasNode));
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnNodeChanged(HabitNode? value) => OnPropertyChanged(nameof(HasNode));

    partial void OnNameChanged(string value)
    {
        if (_loading || Node is null) return;
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            HeaderError = "Name is required";
            HeaderStatus = EditorStatus.Error;
            return;
        }
        if (trimmed.Length > NameMaxLength)
        {
            HeaderError = $"Name too long (max {NameMaxLength})";
            HeaderStatus = EditorStatus.Error;
            return;
        }
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.Name = trimmed).AsTask());
    }

    partial void OnDescriptionChanged(string? value)
    {
        if (_loading || Node is null) return;
        if (value is { Length: > DescriptionMaxLength })
        {
            HeaderError = $"Description too long (max {DescriptionMaxLength})";
            HeaderStatus = EditorStatus.Error;
            return;
        }
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.Description = value).AsTask());
    }

    partial void OnColorChanged(string value)
    {
        if (_loading || Node is null) return;
        if (string.IsNullOrEmpty(value) || !HexColorPattern.IsMatch(value))
        {
            HeaderError = "Color must match #RRGGBB";
            HeaderStatus = EditorStatus.Error;
            return;
        }
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.Color = value).AsTask());
    }

    partial void OnIconChanged(string? value)
    {
        if (_loading || Node is null) return;
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.Icon = value).AsTask());
    }

    partial void OnSelectedNodeTypeChanged(NodeType value)
    {
        if (_loading || Node is null) return;
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.NodeType = value).AsTask());
    }

    partial void OnSelectedHabitTypeChanged(HabitType? value)
    {
        if (_loading || Node is null) return;
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.HabitType = value).AsTask());
    }

    private async Task ApplyHeaderAsync(Func<Task> persist)
    {
        HeaderStatus = EditorStatus.Saving;
        HeaderError = null;
        try
        {
            await persist();
            HeaderStatus = EditorStatus.Saved;
        }
        catch (Exception ex)
        {
            HeaderError = ex.Message;
            HeaderStatus = EditorStatus.Error;
        }
    }
}
