using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HabitScreenViewModel : ObservableObject, IDisposable
{
    private readonly IHabitNodeService _nodes;
    private readonly ICompletionService _completions;
    private readonly INoteService _noteService;
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
    public DetailViewModel Detail { get; }
    public CalendarNavigatorViewModel Calendar { get; }
    public IWebView2Availability Availability { get; }

    [ObservableProperty] private QuickAddViewModelBase? _quickAdd;

    public event EventHandler? ConfigureRequested;
    public event EventHandler? ExportRequested;

    public HabitScreenViewModel(
        IHabitNodeService nodes,
        ICompletionService completions,
        IActiveTimerService timers,
        IMetricDefinitionService metrics,
        IHabitConfigService configs,
        INoteService noteService,
        IGoalService goals,
        IIntensityRampResolver ramp,
        IStreakAggregator streaks,
        ITrendQueryService trend,
        IHourlyDistributionQueryService hourly,
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
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(ramp);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(availability);
        _nodes = nodes;
        _completions = completions;
        _noteService = noteService;
        _metrics = metrics;
        _configs = configs;
        _clock = clock;
        Availability = availability;
        Heatmap = new HeatmapViewModel(completions, clock, ramp, goals, configs);
        CompletionList = new CompletionListViewModel(completions, metrics);
        TimerControl = new TimerControlViewModel(timers, configs, clock);
        Notes = new NotesViewModel(noteService, events, clock, availability);
        Detail = new DetailViewModel(goals, streaks, trend, hourly, completions, metrics, ramp, configs, clock, events);
        Calendar = new CalendarNavigatorViewModel(clock) { MarkerProvider = LoadMonthMarkersAsync };
        Calendar.SelectedDateChanged += OnCalendarDateSelected;
        Heatmap.DateClicked += OnHeatmapDateClicked;
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionRecorded, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionUpdated, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionDeleted, OnCompletionEvent));
    }

    public void Dispose()
    {
        Notes.Dispose();
        Detail.Dispose();
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
        var detailTask = Detail.LoadAsync(Node, ct);
        await Task.WhenAll(configTask, heatmapTask, listTask, quickAddTask, notesTask, detailTask).ConfigureAwait(true);

        Calendar.CompletedColorHex = Node.Color;
        Calendar.NoteColorHex = configTask.Result?.NoteMarkerColor ?? "#e0b341";
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone);
        await Calendar.LoadAsync(todayLocal.Year, todayLocal.Month, DateOnly.FromDateTime(todayLocal), ct).ConfigureAwait(true);

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
            CompletionList.LoadAsync(Node.Id),
            Calendar.JumpAsync())
            .ConfigureAwait(true);
    }

    private void OnHeatmapDateClicked(object? sender, DateOnly date)
    {
        if (QuickAdd is null) return;
        QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
    }

    private async Task<CalendarMonthMarkers> LoadMonthMarkersAsync(int year, int month, CancellationToken ct)
    {
        if (Node is null)
            return new CalendarMonthMarkers(new HashSet<DateOnly>(), new HashSet<DateOnly>());

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var heatmap = await _completions.GetHeatmapAsync(Node.Id, from, to, ct).ConfigureAwait(true);
        var completed = heatmap.Where(c => c.Count > 0).Select(c => c.Date).ToHashSet();

        var noteDays = await _noteService.ListNoteDaysInMonthAsync(Node.Id, year, month, ct).ConfigureAwait(true);
        return new CalendarMonthMarkers(completed, noteDays.ToHashSet());
    }

    private void OnCalendarDateSelected(object? sender, DateOnly date)
    {
        if (QuickAdd is not null)
            QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    private void OpenConfigure() => ConfigureRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Export() => ExportRequested?.Invoke(this, EventArgs.Empty);
}
