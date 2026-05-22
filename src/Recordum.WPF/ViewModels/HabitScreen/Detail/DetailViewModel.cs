using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class DetailViewModel : ObservableObject, IDisposable
{
    private readonly IGoalService _goals;
    private readonly IHabitConfigService _configs;
    private readonly List<IDisposable> _subs = [];

    [ObservableProperty] private HabitNode? _node;
    [ObservableProperty] private bool _isGroup;
    [ObservableProperty] private bool _isHabit;

    public GoalProgressCardViewModel Goal { get; }
    public YearHeatmapViewModel YearHeatmap { get; }
    public StreakSummaryViewModel StreakSummary { get; }
    public StreakTimelineViewModel StreakTimeline { get; }
    public TrendChartViewModel Trend { get; }
    public HourlyHistogramViewModel Hourly { get; }

    public DetailViewModel(
        IGoalService goals,
        IStreakAggregator streaks,
        ITrendQueryService trend,
        IHourlyDistributionQueryService hourly,
        ICompletionService completions,
        IMetricDefinitionService metrics,
        IIntensityRampResolver ramp,
        IHabitConfigService configs,
        IClock clock,
        IEventBus bus)
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(bus);
        _goals = goals;
        _configs = configs;
        Goal = new GoalProgressCardViewModel(goals, completions, clock);
        YearHeatmap = new YearHeatmapViewModel(completions, metrics, ramp, clock);
        StreakSummary = new StreakSummaryViewModel(streaks, clock);
        StreakTimeline = new StreakTimelineViewModel(streaks, clock);
        Trend = new TrendChartViewModel(trend, clock);
        Hourly = new HourlyHistogramViewModel(hourly, clock);
        Trend.RangeChanged += OnTrendRangeChanged;

        _subs.Add(bus.Subscribe(WellKnownEvents.CompletionRecorded, OnCompletionEvent));
        _subs.Add(bus.Subscribe(WellKnownEvents.CompletionUpdated, OnCompletionEvent));
        _subs.Add(bus.Subscribe(WellKnownEvents.CompletionDeleted, OnCompletionEvent));
        _subs.Add(bus.Subscribe(WellKnownEvents.NoteCreated, OnNoteEvent));
        _subs.Add(bus.Subscribe(WellKnownEvents.NoteUpdated, OnNoteEvent));
        _subs.Add(bus.Subscribe(WellKnownEvents.NoteDeleted, OnNoteEvent));
    }

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        IsGroup = node.NodeType == NodeType.Group;
        IsHabit = node.NodeType == NodeType.Habit;
        if (IsGroup)
        {
            await Goal.LoadAsync(node, ct).ConfigureAwait(true);
            return;
        }
        var config = await _configs.GetAsync(node.Id, ct).ConfigureAwait(true);
        var activeGoal = await _goals.GetActiveAsync(node.Id, ct).ConfigureAwait(true);
        await Task.WhenAll(
            Goal.LoadAsync(node, ct),
            YearHeatmap.LoadAsync(node, config, activeGoal, ct),
            StreakSummary.LoadAsync(node, ct),
            StreakTimeline.LoadAsync(node, ct),
            Trend.LoadAsync(node, ct),
            Hourly.LoadAsync(node, ct)
        ).ConfigureAwait(true);
    }

    private async void OnTrendRangeChanged(object? sender, TrendRange range)
    {
        await Hourly.SetRangeAsync(range).ConfigureAwait(true);
    }

    private async void OnCompletionEvent(EventEnvelope envelope)
    {
        if (Node is null) return;
        Guid habitId = envelope.Payload switch
        {
            CompletionRecordedPayload r => r.HabitNodeId,
            CompletionUpdatedPayload u => u.HabitNodeId,
            CompletionDeletedPayload d => d.HabitNodeId,
            _ => Guid.Empty,
        };
        if (habitId != Node.Id) return;
        await LoadAsync(Node).ConfigureAwait(true);
    }

    private async void OnNoteEvent(EventEnvelope envelope)
    {
        if (Node is null || !IsHabit || Node.HabitType != HabitType.Journal) return;
        Guid habitId = envelope.Payload switch
        {
            NoteCreatedPayload r => r.HabitNodeId,
            NoteUpdatedPayload u => u.HabitNodeId,
            NoteDeletedPayload d => d.HabitNodeId,
            _ => Guid.Empty,
        };
        if (habitId != Node.Id) return;
        await Task.WhenAll(Trend.LoadAsync(Node), Goal.LoadAsync(Node)).ConfigureAwait(true);
    }

    public void Dispose()
    {
        Trend.RangeChanged -= OnTrendRangeChanged;
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }
}
