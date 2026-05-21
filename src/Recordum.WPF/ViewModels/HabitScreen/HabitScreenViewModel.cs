using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HabitScreenViewModel : ObservableObject, IDisposable
{
    private readonly IHabitNodeService _nodes;
    private readonly ICompletionService _completions;
    private readonly IActiveTimerService _timers;
    private readonly IMetricDefinitionService _metrics;
    private readonly IHabitConfigService _configs;
    private readonly IClock _clock;
    private readonly IEventBus _events;
    private readonly List<IDisposable> _subscriptions = [];

    [ObservableProperty] private HabitNode? _node;

    public bool HasNode => Node is not null;
    public string Name => Node?.Name ?? string.Empty;

    public HeatmapViewModel Heatmap { get; }
    public CompletionListViewModel CompletionList { get; }

    [ObservableProperty] private QuickAddViewModelBase? _quickAdd;
    public TimerControlViewModel TimerControl { get; }

    public event EventHandler? ConfigureRequested;

    public HabitScreenViewModel(
        IHabitNodeService nodes,
        ICompletionService completions,
        IActiveTimerService timers,
        IMetricDefinitionService metrics,
        IHabitConfigService configs,
        IClock clock,
        IEventBus events)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(timers);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(configs);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        _nodes = nodes;
        _completions = completions;
        _timers = timers;
        _metrics = metrics;
        _configs = configs;
        _clock = clock;
        _events = events;
        Heatmap = new HeatmapViewModel(completions, clock);
        CompletionList = new CompletionListViewModel(completions, metrics);
        TimerControl = new TimerControlViewModel(timers, configs, clock);
        Heatmap.DateClicked += OnHeatmapDateClicked;
        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        _subscriptions.Add(_events.Subscribe(WellKnownEvents.CompletionRecorded, OnCompletionEvent));
        _subscriptions.Add(_events.Subscribe(WellKnownEvents.CompletionUpdated, OnCompletionEvent));
        _subscriptions.Add(_events.Subscribe(WellKnownEvents.CompletionDeleted, OnCompletionEvent));
    }

    private async void OnCompletionEvent(EventEnvelope envelope)
    {
        if (Node is null) return;
        var habitId = envelope.Payload switch
        {
            CompletionRecordedPayload r => r.HabitNodeId,
            CompletionUpdatedPayload u => u.HabitNodeId,
            CompletionDeletedPayload d => d.HabitNodeId,
            _ => Guid.Empty,
        };
        if (habitId != Node.Id) return;
        await Heatmap.LoadAsync(Node.Id).ConfigureAwait(true);
        await CompletionList.LoadAsync(Node.Id).ConfigureAwait(true);
    }

    public void Dispose()
    {
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    public async Task LoadAsync(Guid? habitNodeId, CancellationToken ct = default)
    {
        if (habitNodeId is null)
        {
            DetachQuickAdd();
            Node = null;
            QuickAdd = null;
            OnPropertyChanged(nameof(HasNode));
            OnPropertyChanged(nameof(Name));
            return;
        }

        Node = await _nodes.GetAsync(habitNodeId.Value, ct).ConfigureAwait(true);
        OnPropertyChanged(nameof(HasNode));
        OnPropertyChanged(nameof(Name));
        if (Node is null)
        {
            DetachQuickAdd();
            QuickAdd = null;
            return;
        }

        await Heatmap.LoadAsync(Node.Id, ct).ConfigureAwait(true);
        await CompletionList.LoadAsync(Node.Id, ct).ConfigureAwait(true);
        await BuildQuickAddAsync(Node, ct).ConfigureAwait(true);
        var config = await _configs.GetAsync(Node.Id, ct).ConfigureAwait(true);
        await TimerControl.SetContextAsync(Node, config, ct).ConfigureAwait(true);
    }

    private async Task BuildQuickAddAsync(HabitNode node, CancellationToken ct)
    {
        DetachQuickAdd();
        QuickAddViewModelBase? vm = node.HabitType switch
        {
            HabitType.Action => new ActionQuickAddViewModel(_completions, _clock),
            HabitType.Counter => new CounterQuickAddViewModel(_completions, _metrics, _clock),
            HabitType.Resistance => new ResistanceQuickAddViewModel(_completions, _metrics, _clock),
            HabitType.Journal => new JournalQuickAddViewModel(_completions, _clock),
            HabitType.State => new StateQuickAddViewModel(_completions, _metrics, _clock),
            _ => null,
        };
        if (vm is null) { QuickAdd = null; return; }
        vm.Node = node;
        vm.CompletionSaved += OnCompletionSaved;
        switch (vm)
        {
            case CounterQuickAddViewModel c: await c.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case ResistanceQuickAddViewModel r: await r.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case StateQuickAddViewModel s: await s.LoadMetricsAsync(ct).ConfigureAwait(true); break;
        }
        QuickAdd = vm;
    }

    private void DetachQuickAdd()
    {
        if (QuickAdd is null) return;
        QuickAdd.CompletionSaved -= OnCompletionSaved;
    }

    private async void OnCompletionSaved(object? sender, Guid completionId)
    {
        if (Node is null) return;
        await Heatmap.LoadAsync(Node.Id).ConfigureAwait(true);
        await CompletionList.LoadAsync(Node.Id).ConfigureAwait(true);
        _ = completionId;
    }

    private void OnHeatmapDateClicked(object? sender, DateOnly date)
    {
        if (QuickAdd is null) return;
        QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    private void OpenConfigure() => ConfigureRequested?.Invoke(this, EventArgs.Empty);
}
