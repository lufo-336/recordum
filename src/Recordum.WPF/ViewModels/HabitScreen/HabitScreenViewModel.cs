using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HabitScreenViewModel : ObservableObject, IDisposable
{
    private readonly IHabitNodeService _nodes;
    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;
    private readonly IHabitConfigService _configs;
    private readonly IClock _clock;
    private readonly List<IDisposable> _subscriptions = [];

    [ObservableProperty] private HabitNode? _node;

    public bool HasNode => Node is not null;
    public string Name => Node?.Name ?? string.Empty;

    public HeatmapViewModel Heatmap { get; }
    public CompletionListViewModel CompletionList { get; }
    public TimerControlViewModel TimerControl { get; }
    public NotesViewModel Notes { get; }
    public IWebView2Availability Availability { get; }

    [ObservableProperty] private QuickAddViewModelBase? _quickAdd;

    public event EventHandler? ConfigureRequested;

    public HabitScreenViewModel(
        IHabitNodeService nodes,
        ICompletionService completions,
        IActiveTimerService timers,
        IMetricDefinitionService metrics,
        IHabitConfigService configs,
        INoteService noteService,
        IClock clock,
        IEventBus events,
        IWebView2Availability availability)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(timers);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(configs);
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(availability);
        _nodes = nodes;
        _completions = completions;
        _metrics = metrics;
        _configs = configs;
        _clock = clock;
        Availability = availability;
        Heatmap = new HeatmapViewModel(completions, clock);
        CompletionList = new CompletionListViewModel(completions, metrics);
        TimerControl = new TimerControlViewModel(timers, configs, clock);
        Notes = new NotesViewModel(noteService, events, clock, availability);
        Heatmap.DateClicked += OnHeatmapDateClicked;
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionRecorded, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionUpdated, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionDeleted, OnCompletionEvent));
    }

    public void Dispose()
    {
        Notes.Dispose();
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    public async Task LoadAsync(Guid? habitNodeId, CancellationToken ct = default)
    {
        if (habitNodeId is null)
        {
            Node = null;
            QuickAdd = null;
            OnPropertyChanged(nameof(HasNode));
            OnPropertyChanged(nameof(Name));
            return;
        }

        Node = await _nodes.GetAsync(habitNodeId.Value, ct).ConfigureAwait(true);
        OnPropertyChanged(nameof(HasNode));
        OnPropertyChanged(nameof(Name));
        if (Node is null) { QuickAdd = null; return; }

        // Config is needed for both QuickAdd and TimerControl; fetch once, share.
        var configTask = _configs.GetAsync(Node.Id, ct).AsTask();
        var heatmapTask = Heatmap.LoadAsync(Node.Id, ct);
        var listTask = CompletionList.LoadAsync(Node.Id, ct);
        var quickAddTask = BuildQuickAddAsync(Node, ct);
        var notesTask = Notes.LoadAsync(Node.Id, ct);
        await Task.WhenAll(configTask, heatmapTask, listTask, quickAddTask, notesTask).ConfigureAwait(true);

        await TimerControl.SetContextAsync(Node, configTask.Result, ct).ConfigureAwait(true);
    }

    private async Task BuildQuickAddAsync(HabitNode node, CancellationToken ct)
    {
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
        switch (vm)
        {
            case CounterQuickAddViewModel c: await c.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case ResistanceQuickAddViewModel r: await r.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case StateQuickAddViewModel s: await s.LoadMetricsAsync(ct).ConfigureAwait(true); break;
        }
        QuickAdd = vm;
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
        await Task.WhenAll(
            Heatmap.LoadAsync(Node.Id),
            CompletionList.LoadAsync(Node.Id))
            .ConfigureAwait(true);
    }

    private void OnHeatmapDateClicked(object? sender, DateOnly date)
    {
        if (QuickAdd is null) return;
        QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    private void OpenConfigure() => ConfigureRequested?.Invoke(this, EventArgs.Empty);
}
