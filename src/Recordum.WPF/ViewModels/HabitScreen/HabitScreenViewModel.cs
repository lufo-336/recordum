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
    private readonly IReferenceDataQueryService _refData;
    private readonly IHabitConfigService _configs;
    private readonly IClock _clock;
    private readonly List<IDisposable> _subscriptions = [];

    [ObservableProperty] private HabitNode? _node;

    public bool HasNode => Node is not null;
    public string Name => Node?.Name ?? string.Empty;

    public CompletionListViewModel CompletionList { get; }

    /// <summary>Completions recorded on the calendar-selected day, with readable field values (B12).
    /// Shown in the Abitudine tab; reloaded on day change and on completion events.</summary>
    public CompletionListViewModel DayCompletions { get; }
    public TimerControlViewModel TimerControl { get; }
    public NotesViewModel Notes { get; }
    public DetailViewModel Detail { get; }
    public CalendarNavigatorViewModel Calendar { get; }
    public DayNoteViewModel DayNote { get; }
    public IWebView2Availability Availability { get; }

    [ObservableProperty] private QuickAddViewModelBase? _quickAdd;

    public event EventHandler? ConfigureRequested;
    public event EventHandler? ExportRequested;

    public HabitScreenViewModel(
        IHabitNodeService nodes,
        ICompletionService completions,
        IActiveTimerService timers,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
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
        ArgumentNullException.ThrowIfNull(refData);
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
        _refData = refData;
        _configs = configs;
        _clock = clock;
        Availability = availability;
        CompletionList = new CompletionListViewModel(completions, metrics, refData);
        DayCompletions = new CompletionListViewModel(completions, metrics, refData);
        TimerControl = new TimerControlViewModel(timers, configs, clock);
        Notes = new NotesViewModel(noteService, events, clock, availability);
        Detail = new DetailViewModel(goals, streaks, trend, hourly, completions, metrics, ramp, configs, clock, events);
        Calendar = new CalendarNavigatorViewModel(clock) { MarkerProvider = LoadMonthMarkersAsync };
        Calendar.SelectedDateChanged += OnCalendarDateSelected;
        DayNote = new DayNoteViewModel(noteService, clock);
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionRecorded, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionUpdated, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.CompletionDeleted, OnCompletionEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.NoteCreated, OnNoteEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.NoteUpdated, OnNoteEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.NoteDeleted, OnNoteEvent));
    }

    public void Dispose()
    {
        Calendar.SelectedDateChanged -= OnCalendarDateSelected;
        Notes.Dispose();
        Detail.Dispose();
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    // Bumped on every LoadAsync. Selecting/deselecting habits fires overlapping async loads
    // (OnHomePropertyChanged is async void with no cancellation); a stale load that resumes after
    // a newer one must not clobber state or dereference a Node the newer load already nulled.
    private int _loadGeneration;

    public async Task LoadAsync(Guid? habitNodeId, CancellationToken ct = default)
    {
        var generation = ++_loadGeneration;

        if (habitNodeId is null)
        {
            Node = null;
            QuickAdd = null;
            OnPropertyChanged(nameof(HasNode));
            OnPropertyChanged(nameof(Name));
            return;
        }

        var node = await _nodes.GetAsync(habitNodeId.Value, ct).ConfigureAwait(true);
        if (generation != _loadGeneration) return; // superseded by a newer load/deselect
        Node = node;
        OnPropertyChanged(nameof(HasNode));
        OnPropertyChanged(nameof(Name));
        if (node is null) { QuickAdd = null; return; }

        // Config is needed for both QuickAdd and TimerControl; fetch once, share.
        var configTask = _configs.GetAsync(node.Id, ct).AsTask();
        var listTask = CompletionList.LoadAsync(node.Id, ct);
        var quickAddTask = BuildQuickAddAsync(node, ct);
        var notesTask = Notes.LoadAsync(node.Id, ct);
        var detailTask = Detail.LoadAsync(node, ct);
        await Task.WhenAll(configTask, listTask, quickAddTask, notesTask, detailTask).ConfigureAwait(true);
        if (generation != _loadGeneration) return; // a deselect/newer selection won the race

        Calendar.CompletedColorHex = node.Color;
        Calendar.NoteColorHex = configTask.Result?.NoteMarkerColor ?? "#e0b341";
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone);
        await Calendar.LoadAsync(todayLocal.Year, todayLocal.Month, DateOnly.FromDateTime(todayLocal), ct).ConfigureAwait(true);
        if (generation != _loadGeneration) return;

        // Mirror the calendar's pre-selected day into QuickAdd so both controls agree before the first click.
        if (QuickAdd is not null && Calendar.SelectedDate is { } preSelected)
            QuickAdd.CompletionDate = preSelected.ToDateTime(TimeOnly.MinValue);

        DayNote.SetContext(node.Id);
        if (Calendar.SelectedDate is { } sel)
        {
            await DayNote.LoadDayAsync(sel, ct).ConfigureAwait(true);
            await PushDaySummaryAsync(sel, ct).ConfigureAwait(true);
            await DayCompletions.LoadDayAsync(node.Id, sel, ct).ConfigureAwait(true);
        }

        await TimerControl.SetContextAsync(node, configTask.Result, ct).ConfigureAwait(true);
    }

    private async Task BuildQuickAddAsync(HabitNode node, CancellationToken ct)
    {
        QuickAddViewModelBase? vm = node.HabitType switch
        {
            HabitType.Action => new ActionQuickAddViewModel(_completions, _clock),
            HabitType.Counter => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock),
            HabitType.Resistance => new ResistanceQuickAddViewModel(_completions, _metrics, _refData, _clock),
            HabitType.Journal => new JournalQuickAddViewModel(_completions, _clock),
            HabitType.State => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock),
            _ => null,
        };
        if (vm is null) { QuickAdd = null; return; }
        vm.Node = node;
        switch (vm)
        {
            case MeasuredQuickAddViewModel mq: await mq.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case ResistanceQuickAddViewModel r: await r.LoadMetricsAsync(ct).ConfigureAwait(true); break;
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
        var dayReload = Calendar.SelectedDate is { } sel
            ? DayCompletions.LoadDayAsync(Node.Id, sel)
            : Task.CompletedTask;
        await Task.WhenAll(
            CompletionList.LoadAsync(Node.Id),
            dayReload,
            Calendar.JumpAsync())
            .ConfigureAwait(true);
    }

    private async void OnNoteEvent(EventEnvelope envelope)
    {
        if (Node is null) return;
        var habitId = envelope.Payload switch
        {
            NoteCreatedPayload c => c.HabitNodeId,
            NoteUpdatedPayload u => u.HabitNodeId,
            NoteDeletedPayload d => d.HabitNodeId,
            _ => Guid.Empty,
        };
        if (habitId != Node.Id) return;
        await Calendar.JumpAsync().ConfigureAwait(true);
    }

    private async Task<CalendarMonthMarkers> LoadMonthMarkersAsync(int year, int month, CancellationToken ct)
    {
        var node = Node;
        if (node is null) return new CalendarMonthMarkers([]);

        var mags = await _completions.GetDailyMagnitudeAsync(node.Id, year, month, ct).ConfigureAwait(true);
        var notes = await _noteService.ListNoteCountsInMonthAsync(node.Id, year, month, ct).ConfigureAwait(true);
        var magByDate = mags.ToDictionary(m => m.Date, m => m.Magnitude);
        var noteByDate = notes.ToDictionary(n => n.Date, n => n.Count);

        var monthMax = mags.Count > 0 ? mags.Max(m => m.Magnitude) : 0m;
        var type = node.HabitType ?? HabitType.Action;

        var days = new List<CalendarDayMarker>();
        var allDates = magByDate.Keys.Union(noteByDate.Keys).Distinct();
        foreach (var date in allDates)
        {
            var mag = magByDate.TryGetValue(date, out var mv) ? mv : 0m;
            var noteCount = noteByDate.TryGetValue(date, out var nc) ? nc : 0;
            days.Add(new CalendarDayMarker(date, TintStepFor(type, mag, monthMax), NumberFor(type, mag), noteCount));
        }
        return new CalendarMonthMarkers(days);
    }

    private static int TintStepFor(HabitType type, decimal mag, decimal monthMax)
    {
        if (mag <= 0) return 0;
        if (type is HabitType.Action or HabitType.Journal) return 4; // binario: fatto = pieno
        if (monthMax <= 0) return 1;
        var step = (int)Math.Ceiling((double)(mag / monthMax) * 4);
        return Math.Clamp(step, 1, 4);
    }

    private static string? NumberFor(HabitType type, decimal mag)
    {
        if (mag <= 0) return null;
        return type switch
        {
            HabitType.Counter or HabitType.State => FormatMag(mag),
            HabitType.Resistance => mag > 1 ? FormatMag(mag) : null,
            _ => mag > 1 ? $"{FormatMag(mag)}×" : null, // Action/Journal: hidden unless multiple
        };
    }

    private static string FormatMag(decimal mag) =>
        mag == Math.Truncate(mag)
            ? ((long)mag).ToString(System.Globalization.CultureInfo.CurrentCulture)
            : mag.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);

    private async void OnCalendarDateSelected(object? sender, DateOnly date)
    {
        if (QuickAdd is not null)
            QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
        await DayNote.LoadDayAsync(date).ConfigureAwait(true);
        await PushDaySummaryAsync(date, CancellationToken.None).ConfigureAwait(true);
        if (Node is { } n) await DayCompletions.LoadDayAsync(n.Id, date).ConfigureAwait(true);
    }

    private async Task PushDaySummaryAsync(DateOnly date, CancellationToken ct)
    {
        var node = Node;
        if (node is null) return;
        var mags = await _completions.GetDailyMagnitudeAsync(node.Id, date.Year, date.Month, ct).ConfigureAwait(true);
        var mag = mags.FirstOrDefault(m => m.Date == date)?.Magnitude ?? 0m;
        // Enrich the aggregate with the primary metric's unit (Counter) or scale label (State) so the
        // strip reads "4 km" / "Good" instead of a bare number (R-daySummaryUnits).
        var (unit, scaleLabels) = await ResolvePrimaryDisplayAsync(node, ct).ConfigureAwait(true);
        var label = DaySummaryFormatter.Label(node.HabitType ?? HabitType.Action, mag, unit, scaleLabels);
        var notes = await _noteService.ListByHabitAndDateAsync(node.Id, date, ct).ConfigureAwait(true);
        DayNote.SetDaySummary(mag > 0, label, notes.Count);
    }

    /// <summary>Resolves the primary (lowest SortOrder) metric's unit code (Counter) or scale labels
    /// (State) for the day-summary strip. Returns nulls for other types or when none apply.</summary>
    private async Task<(string? unit, IReadOnlyList<string>? scaleLabels)> ResolvePrimaryDisplayAsync(HabitNode node, CancellationToken ct)
    {
        var type = node.HabitType ?? HabitType.Action;
        if (type is not (HabitType.Counter or HabitType.State)) return (null, null);

        var metrics = await _metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(true);
        var primary = metrics.OrderBy(m => m.SortOrder).FirstOrDefault();
        if (primary is null) return (null, null);

        if (type == HabitType.Counter)
        {
            if (primary.UnitOfMeasureId is not { } uid) return (null, null);
            var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
            return (units.FirstOrDefault(u => u.Id == uid)?.Code, null);
        }

        // State
        if (primary.ScaleTemplateId is not { } sid) return (null, null);
        var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
        var tmpl = scales.FirstOrDefault(s => s.Id == sid);
        var labels = ParseScaleLabels(primary.LabelsJson) ?? ParseScaleLabels(tmpl?.LabelsJson);
        return (null, labels);
    }

    private static string[]? ParseScaleLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    [RelayCommand]
    private void OpenConfigure() => ConfigureRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Export() => ExportRequested?.Invoke(this, EventArgs.Empty);
}
