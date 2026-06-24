using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;
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
    private readonly IRecentColorStore _recentColors;
    private readonly DebouncedColorRecorder _recordColor;
    private readonly IFavoriteColorStore _favoriteColors;
    private readonly IScreenColorPicker _screenPicker;
    private readonly IEventBus? _events;
    private bool _loading;
    // Bumped each LoadAsync; a load that finds it superseded bails before mutating shared state, so
    // rapidly switching the configured habit can't interleave two loads into a mixed editor (CR14).
    private int _loadGeneration;

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
    public NotesSectionViewModel Notes { get; }
    public TagsSectionViewModel Tags { get; }
    public AiSharingSectionViewModel AiSharing { get; }
    public DataSourceSectionViewModel DataSource { get; }
    public ResetTimeSectionViewModel ResetTime { get; }

    public bool HasNode => Node is not null;

    /// <summary>Recently picked habit colors, loaded from <see cref="IRecentColorStore"/> during
    /// <see cref="LoadAsync"/>. Bound to the <c>ColorSwatchPicker.RecentColors</c> DP.</summary>
    public ObservableCollection<string> RecentColors { get; } = new();

    /// <summary>User-pinned favourite colours (bound to ColorSwatchPicker.FavoriteColors).</summary>
    public ObservableCollection<string> FavoriteColors { get; } = new();

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
        IReferenceDataQueryService refData,
        IRecentColorStore recentColors,
        IFavoriteColorStore favoriteColors,
        IScreenColorPicker screenPicker,
        IEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(configs);
        ArgumentNullException.ThrowIfNull(recentColors);
        ArgumentNullException.ThrowIfNull(favoriteColors);
        ArgumentNullException.ThrowIfNull(screenPicker);
        _nodes = nodes;
        _configs = configs;
        _recentColors = recentColors;
        _recordColor = new DebouncedColorRecorder(recentColors);
        _favoriteColors = favoriteColors;
        _screenPicker = screenPicker;
        _events = events;

        Scheduling = new SchedulingSectionViewModel(configs);
        Streak = new StreakSectionViewModel(configs);
        Timer = new TimerSectionViewModel(configs);
        Metrics = new MetricsSectionViewModel(metrics, refData);
        Scales = new ScalesSectionViewModel(metrics, refData);
        Goal = new GoalSectionViewModel(goals);
        Notifications = new NotificationsSectionViewModel(notifications);
        Notes = new NotesSectionViewModel(configs, recentColors, favoriteColors, _screenPicker);
        Tags = new TagsSectionViewModel(tags);
        AiSharing = new AiSharingSectionViewModel(nodes);
        DataSource = new DataSourceSectionViewModel(appSettings);
        ResetTime = new ResetTimeSectionViewModel(nodes);

        Sections = new SectionViewModelBase[]
        {
            Scheduling, Streak, Timer, Metrics, Scales, Goal, Notifications, Notes, Tags, AiSharing, ResetTime,
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
        yield return Notes;
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
        var generation = ++_loadGeneration;
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
            if (generation != _loadGeneration) return; // superseded by a newer load/deselect
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
            if (generation != _loadGeneration) return; // a newer load won the race before we touched the sections
            await Scheduling.LoadAsync(node, cfg);
            await Streak.LoadAsync(node, cfg);
            await Timer.LoadAsync(node, cfg);
            await Metrics.LoadAsync(node);
            await Scales.LoadAsync(node);
            await Goal.LoadAsync(node);
            await Notifications.LoadAsync(node);
            await Notes.LoadAsync(node, cfg);
            await Tags.LoadAsync(node);
            await AiSharing.LoadAsync(node);
            await DataSource.LoadAsync(node, cfg);
            await ResetTime.LoadAsync(node);

            RefreshSectionVisibility();

            RecentColors.Clear();
            foreach (var c in await _recentColors.GetAsync(ct).ConfigureAwait(true)) RecentColors.Add(c);
            await ReloadFavoritesAsync(ct).ConfigureAwait(true);
            if (generation != _loadGeneration) return; // bail before finalizing a stale load

            HeaderStatus = EditorStatus.Idle;
            HeaderError = null;
            OnPropertyChanged(nameof(HasNode));
        }
        finally
        {
            // Only the current load owns _loading — a superseded one must not clear it for the newer load.
            if (generation == _loadGeneration) _loading = false;
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
        _recordColor.Record(value); // debounced: only the settled colour reaches the recents MRU (CR23)
    }

    partial void OnIconChanged(string? value)
    {
        if (_loading || Node is null) return;
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.Icon = value).AsTask());
    }

    partial void OnSelectedNodeTypeChanged(NodeType value)
    {
        if (_loading || Node is null) return;
        RefreshSectionVisibility();
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.NodeType = value).AsTask());
    }

    partial void OnSelectedHabitTypeChanged(HabitType? value)
    {
        if (_loading || Node is null) return;
        RefreshSectionVisibility();
        _ = ApplyHeaderAsync(() => _nodes.UpdateAsync(Node.Id, n => n.HabitType = value).AsTask());
    }

    /// <summary>
    /// Recomputes each section's <see cref="SectionViewModelBase.IsVisible"/> from the current
    /// node/habit type. Called at load and whenever the type changes in the editor, so type-specific
    /// sections (Metrics/Scales/Timer) appear/disappear immediately without reopening the editor.
    /// </summary>
    private void RefreshSectionVisibility()
    {
        foreach (var s in Sections)
            s.IsVisible = s.IsApplicable(SelectedNodeType, SelectedHabitType);
    }

    [RelayCommand]
    private async Task AddFavoriteAsync(string? hex, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        await _favoriteColors.AddAsync(hex, ct).ConfigureAwait(true);
        await ReloadFavoritesAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveFavoriteAsync(string? hex, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        await _favoriteColors.RemoveAsync(hex, ct).ConfigureAwait(true);
        await ReloadFavoritesAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EyedropAsync()
    {
        var picked = await _screenPicker.PickAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(picked)) Color = picked;
    }

    private async Task ReloadFavoritesAsync(CancellationToken ct = default)
    {
        FavoriteColors.Clear();
        foreach (var c in await _favoriteColors.GetAsync(ct).ConfigureAwait(true)) FavoriteColors.Add(c);
    }

    private async Task ApplyHeaderAsync(Func<Task> persist)
    {
        HeaderStatus = EditorStatus.Saving;
        HeaderError = null;
        try
        {
            await persist();
            HeaderStatus = EditorStatus.Saved;
            // Notify views holding a cached copy (Home rows, the open workspace) so name/color/icon
            // changes reflect live instead of only after a reload.
            if (Node is { } n) _events?.Publish(new HabitUpdatedPayload(n.Id));
        }
        catch (Exception ex)
        {
            HeaderError = ex.Message;
            HeaderStatus = EditorStatus.Error;
        }
    }
}
