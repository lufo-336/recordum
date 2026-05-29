using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
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

    public IReadOnlyList<NodeType> AvailableNodeTypes { get; } = new[] { NodeType.Habit, NodeType.Group };
    public IReadOnlyList<HabitType> AvailableHabitTypes { get; } = new[]
    {
        HabitType.Action, HabitType.Counter, HabitType.Resistance, HabitType.Journal, HabitType.State,
    };

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

    /// <summary>
    /// Ordered list of sections to render in the editor pane. DataSource is intentionally excluded
    /// (it is an internal/config-level section handled separately).
    /// </summary>
    public IReadOnlyList<SectionViewModelBase> Sections { get; }

    public HabitEditorViewModel(
        IHabitNodeService nodes,
        IHabitConfigService configs,
        IGoalService goals,
        IMetricDefinitionService metrics,
        INotificationService notifications,
        ITagService tags,
        IAppSettingService appSettings,
        IReferenceDataQueryService refData)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(configs);
        _nodes = nodes;
        _configs = configs;

        Scheduling = new SchedulingSectionViewModel(configs);
        Streak = new StreakSectionViewModel(configs);
        Timer = new TimerSectionViewModel(configs);
        Metrics = new MetricsSectionViewModel(metrics, refData);
        Scales = new ScalesSectionViewModel(metrics, refData);
        Goal = new GoalSectionViewModel(goals);
        Notifications = new NotificationsSectionViewModel(notifications);
        Tags = new TagsSectionViewModel(tags);
        AiSharing = new AiSharingSectionViewModel(nodes);
        DataSource = new DataSourceSectionViewModel(appSettings);
        ResetTime = new ResetTimeSectionViewModel(nodes);

        Sections = new SectionViewModelBase[]
        {
            Scheduling, Streak, Timer, Metrics, Scales, Goal, Notifications, Tags, AiSharing, ResetTime,
        };

        foreach (var section in EnumerateSections())
            section.PropertyChanged += OnSectionPropertyChanged;
    }

    private IEnumerable<SectionViewModelBase> EnumerateSections()
    {
        yield return Scheduling;
        yield return Streak;
        yield return Timer;
        yield return Metrics;
        yield return Scales;
        yield return Goal;
        yield return Notifications;
        yield return Tags;
        yield return AiSharing;
        yield return DataSource;
        yield return ResetTime;
    }

    private void OnSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SectionViewModelBase.Status) or nameof(SectionViewModelBase.ErrorMessage))
            OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    /// Footer status text: severity-max across header + every section.
    /// Order: Error &gt; Saving &gt; Saved &gt; Idle. On Error, includes the first error message after the localized prefix.
    /// </summary>
    public string StatusText
    {
        get
        {
            var (status, error) = AggregateStatus();
            return status switch
            {
                EditorStatus.Error => string.IsNullOrEmpty(error)
                    ? Strings.Editor_Status_Error
                    : $"{Strings.Editor_Status_Error}: {error}",
                EditorStatus.Saving => Strings.Editor_Status_Saving,
                EditorStatus.Saved => Strings.Editor_Status_Saved,
                _ => string.Empty,
            };
        }
    }

    private (EditorStatus Status, string? Error) AggregateStatus()
    {
        if (HeaderStatus == EditorStatus.Error) return (EditorStatus.Error, HeaderError);
        foreach (var s in EnumerateSections())
            if (s.Status == EditorStatus.Error) return (EditorStatus.Error, s.ErrorMessage);

        if (HeaderStatus == EditorStatus.Saving) return (EditorStatus.Saving, null);
        foreach (var s in EnumerateSections())
            if (s.Status == EditorStatus.Saving) return (EditorStatus.Saving, null);

        if (HeaderStatus == EditorStatus.Saved) return (EditorStatus.Saved, null);
        foreach (var s in EnumerateSections())
            if (s.Status == EditorStatus.Saved) return (EditorStatus.Saved, null);

        return (EditorStatus.Idle, null);
    }

    partial void OnHeaderStatusChanged(EditorStatus value) => OnPropertyChanged(nameof(StatusText));
    partial void OnHeaderErrorChanged(string? value) => OnPropertyChanged(nameof(StatusText));

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

            foreach (var s in Sections)
                s.IsVisible = s.IsApplicable(node.NodeType, node.HabitType);

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
            HeaderError = Strings.Editor_Error_NameRequired;
            HeaderStatus = EditorStatus.Error;
            return;
        }
        if (trimmed.Length > NameMaxLength)
        {
            HeaderError = Strings.Editor_Error_NameTooLong;
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
            HeaderError = Strings.Editor_Error_DescriptionTooLong;
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
            HeaderError = Strings.Editor_Error_ColorInvalid;
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
