using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;
using Recordum.Presentation.ViewModels.HabitScreen.Group;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>Group overview column: today rollup, descendant habit rows, group goal (spec §A.2).</summary>
public sealed partial class PanoramicaViewModel : ObservableObject, IDisposable
{
    private readonly IHabitTreeQueryService _tree;
    private readonly IHabitNodeService? _nodes;
    private readonly IPurgeService? _purge;
    private readonly IGroupHistoryQueryService? _history;
    private readonly IHabitUiPrefsService? _uiPrefs;
    private readonly IStreakAggregator? _streaks;
    private readonly QuickActionHandlerSelector? _quickActions;
    private readonly IResetTimeResolver? _resetTimes;
    private readonly IClock _clock;
    private readonly HomeClockTicker _clockTicker;
    private int _generation;
    private HabitNode? _group;
    private DayBoundary _day = DayBoundary.Utc;
    private Guid _groupId;

    /// <summary>Le figlie come righe della Home (stesso comando di registrazione rapida), in ordine d'albero, senza archiviate.
    /// Vuote dove l'host non fornisce i servizi (WPF).</summary>
    public ObservableCollection<HomeRowViewModel> Rows { get; } = [];

    /// <summary>"Insieme": una riga per figlia, una cella per giorno del periodo.</summary>
    public TogetherViewModel Together { get; }

    /// <summary>La mappa dell'anno del gruppo: per giorno quante figlie previste sono fatte.</summary>
    public HistoryMapViewModel Map { get; }

    public GoalProgressCardViewModel Goal { get; }
    public ObservableCollection<PanoramicaChildRow> Children { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodayLabel))]
    [NotifyPropertyChangedFor(nameof(TodayFraction))]
    private int _totalChildren;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodayLabel))]
    [NotifyPropertyChangedFor(nameof(TodayFraction))]
    private int _completedToday;

    /// <summary>La frazione di oggi (0–1) per la barra in testa; 0 senza abitudini.</summary>
    public double TodayFraction => TotalChildren == 0 ? 0 : (double)CompletedToday / TotalChildren;

    /// <summary>True when the rollup has any row (habit OR subgroup) — drives the empty-state.</summary>
    public bool HasChildren => Children.Count > 0;

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
    public string TodayLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Resources.Strings.HabitScreen_Panoramica_Today, CompletedToday, TotalChildren);
#pragma warning restore CA1863

    /// <summary>Click on a child row: the host selects that habit in the Home tree (spec §A.3).</summary>
    public event EventHandler<Guid>? ChildSelected;

    public PanoramicaViewModel(
        IHabitTreeQueryService tree,
        IGoalService goals,
        ICompletionService completions,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
        IClock clock)
    {
        _tree = tree;
        _clock = clock;
        _clockTicker = new HomeClockTicker(null);
        Goal = new GoalProgressCardViewModel(goals, completions, metrics, refData, clock);
        Together = new TogetherViewModel(null);
        Map = new HistoryMapViewModel(LoadGroupMarkersAsync);
    }

    /// <summary>Con i servizi della Home: le figlie diventano <see cref="Rows"/> segnabili, e "Insieme" e la mappa si caricano.</summary>
    public PanoramicaViewModel(
        IHabitTreeQueryService tree,
        IGoalService goals,
        ICompletionService completions,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
        IClock clock,
        IHabitNodeService nodes,
        IPurgeService purge,
        IGroupHistoryQueryService history,
        IHabitUiPrefsService? uiPrefs = null,
        IStreakAggregator? streaks = null,
        QuickActionHandlerSelector? quickActions = null,
        IResetTimeResolver? resetTimes = null,
        ITickSourceFactory? ticks = null)
    {
        _tree = tree;
        _clock = clock;
        _nodes = nodes;
        _purge = purge;
        _history = history;
        _uiPrefs = uiPrefs;
        _streaks = streaks;
        _quickActions = quickActions;
        _resetTimes = resetTimes;
        _clockTicker = new HomeClockTicker(ticks);
        Goal = new GoalProgressCardViewModel(goals, completions, metrics, refData, clock);
        Together = new TogetherViewModel(history);
        Map = new HistoryMapViewModel(LoadGroupMarkersAsync);
    }

    internal HomeClockTicker ClockTicker => _clockTicker;

    public void Dispose()
    {
        UnhookRows();
        _clockTicker.Dispose();
    }

    private void UnhookRows()
    {
        foreach (var old in Rows)
        {
            old.Changed -= OnRowChanged;
            old.QuickActionExecuted -= OnRowQuickActionExecuted;
        }
    }

    // Una riga archiviata/spostata/eliminata dal suo menu: si rilegge il gruppo.
    private void OnRowChanged(object? sender, EventArgs e) => _ = ReloadAsync();

    private async Task ReloadAsync()
    {
        try { if (_group is { } g) await LoadAsync(g, _day).ConfigureAwait(true); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Panoramica reload failed: {ex}"); }
    }

    // Registrata: l'evento del bus ricarica (HabitScreenViewModel). Altrimenti la registrazione rapida non basta: si apre la figlia.
    private void OnRowQuickActionExecuted(object? sender, QuickActionExecutedEventArgs e)
    {
        if (!e.Recorded) ChildSelected?.Invoke(this, e.HabitNodeId);
    }

    /// <summary>Quante figlie previste sono fatte, per giorno: 0 nessuna, 4 tutte, in mezzo <c>1 + floor(3 * fatte / previste)</c> (al più 3).</summary>
    private async Task<CalendarMarkers> LoadGroupMarkersAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (_history is null) return new CalendarMarkers([]);
        var children = await _history.GetAsync(_groupId, from, to, ct).ConfigureAwait(true);
        var days = new List<CalendarDayMarker>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var due = children.Where(c => c.Scheduled.Contains(d)).ToList();
            var done = due.Count(c => c.Done.Contains(d));
            if (done == 0) continue;
            var step = done == due.Count ? 4 : Math.Min(3, 1 + 3 * done / due.Count);
            days.Add(new CalendarDayMarker(d, step, null, 0));
        }
        return new CalendarMarkers(days);
    }

    /// <summary>Carica ciò che segue il periodo: "Insieme" e la mappa del gruppo.</summary>
    public Task LoadHistoryAsync(DateOnly from, DateOnly to, MapShape shape, DateOnly today, CancellationToken ct = default)
        => _history is null
            ? Task.CompletedTask
            : Task.WhenAll(
                Together.LoadAsync(_groupId, from, to, today, Rows.Select(r => r.HabitNodeId).ToList(), ct),
                Map.LoadAsync(from, to, shape, today, null, ct));

    /// <summary>Il confine con cui è stato fatto l'ultimo caricamento. Serve al test di I1: senza
    /// poterlo leggere, "il gruppo ha ricevuto il confine giusto" non è una cosa che si possa provare —
    /// il campo dello screen a fine caricamento è corretto comunque, ed è proprio ciò che rendeva il
    /// difetto invisibile.</summary>
    internal DayBoundary? LastLoadedBoundary { get; private set; }

    public async Task LoadAsync(HabitNode group, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        var generation = ++_generation;
        LastLoadedBoundary = day;
        _group = group;
        _day = day;
        _groupId = group.Id;
        var rows = await _tree.GetHomeTreeAsync(ct).ConfigureAwait(true);
        // Le archiviate non sono nel conto: nemmeno nell'elenco.
        var live = rows.Where(r => !r.IsArchived).ToList();
        var byParent = live.ToLookup(r => r.ParentId);
        var prefs = await PrefetchPrefsAsync(byParent, group.Id).ConfigureAwait(true);
        if (generation != _generation) return; // un caricamento più nuovo ha vinto: non lo si sovrascrive
        Children.Clear();
        var (completedCount, habitCount) = GroupProgress.Count(byParent, group.Id);

        // Hierarchical rollup: subgroups appear as their own (indented) rows, habits beneath
        // them. The today counter comes from GroupProgress.
        void Walk(Guid parentId, int indent)
        {
            foreach (var child in byParent[parentId])
            {
                if (child.HabitType is null)
                {
                    Children.Add(new PanoramicaChildRow(
                        child.HabitNodeId, child.Name, child.ColorHex, false,
                        child.CompletionsLast7Days, IsGroup: true, Indent: indent, ChildCount: child.ChildCount));
                    Walk(child.HabitNodeId, indent + 1);
                }
                else
                {
                    Children.Add(new PanoramicaChildRow(
                        child.HabitNodeId, child.Name, child.ColorHex, GroupProgress.IsDoneToday(child),
                        child.CompletionsLast7Days, Indent: indent));
                }
            }
        }

        Walk(group.Id, 0);
        TotalChildren = habitCount;
        CompletedToday = completedCount;
        OnPropertyChanged(nameof(HasChildren));
        RebuildRows(byParent, group.Id, prefs);
        await Goal.LoadAsync(group, day, ct).ConfigureAwait(true);
        if (generation != _generation) return;
        await FillKeyFiguresAsync(generation).ConfigureAwait(true);
    }

    private async Task<Dictionary<Guid, HabitUiPrefs>> PrefetchPrefsAsync(ILookup<Guid?, HomeTreeRow> byParent, Guid groupId)
    {
        if (_uiPrefs is null || _nodes is null) return [];
        var ids = Descendants(byParent, groupId).Where(r => r.HabitType is not null).Select(r => r.HabitNodeId).Distinct().ToList();
        var pairs = await Task.WhenAll(ids.Select(async id => (id, prefs: await _uiPrefs.GetAsync(id).ConfigureAwait(true)))).ConfigureAwait(true);
        return pairs.ToDictionary(p => p.id, p => p.prefs);
    }

    private static IEnumerable<HomeTreeRow> Descendants(ILookup<Guid?, HomeTreeRow> byParent, Guid parent)
        => byParent[parent].SelectMany(c => Descendants(byParent, c.HabitNodeId).Prepend(c));

    // Come HomeViewModel.Rebuild: indentazione, preferenze, comando rapido, orologio vivo dell'astinenza. Niente cornici.
    private void RebuildRows(ILookup<Guid?, HomeTreeRow> byParent, Guid groupId, Dictionary<Guid, HabitUiPrefs> prefs)
    {
        UnhookRows();
        Rows.Clear();
        _clockTicker.Clear();
        if (_nodes is null || _purge is null) return;
        IEnumerable<(HomeTreeRow Row, int Level)> Order(Guid parent, int level)
            => byParent[parent]
                .OrderByDescending(r => r.PinScope == PinScope.Local)
                .ThenBy(r => r.SortOrder)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .SelectMany(r => Order(r.HabitNodeId, level + 1).Prepend((r, level)));
        foreach (var (row, level) in Order(groupId, 0))
        {
            prefs.TryGetValue(row.HabitNodeId, out var p);
            var vm = new HomeRowViewModel(row, level, _nodes, _purge, p, _streaks, _clock) { CurrentTab = HomeTab.Active };
            if (_quickActions is not null) vm.AttachQuickActionSelector(_quickActions);
            if (vm.IsResistanceClock) _clockTicker.Register(vm.RefreshClock);
            vm.Changed += OnRowChanged;
            vm.QuickActionExecuted += OnRowQuickActionExecuted;
            Rows.Add(vm);
        }
        foreach (var g in Rows.Where(r => r.IsGroup))
        {
            var (done, total) = GroupProgress.Count(byParent, g.HabitNodeId);
            g.SetGroupProgress(done, total);
        }
    }

    // La cifra chiave (serie) arriva dopo il disegno, col confine del giorno di ogni figlia (batch), come nella Home.
    private async Task FillKeyFiguresAsync(int generation)
    {
        if (_streaks is null) return;
        var rows = Rows.Where(r => r.KeyFigureKind == KeyFigureKind.Streak).ToList();
        if (rows.Count == 0) return;
        var appDay = _resetTimes is null ? DayBoundary.Utc : await _resetTimes.GetAppDayBoundaryAsync().ConfigureAwait(true);
        var boundaries = _resetTimes is null
            ? new Dictionary<Guid, DayBoundary>()
            : (IReadOnlyDictionary<Guid, DayBoundary>)await _resetTimes
                .GetDayBoundariesAsync(rows.Select(r => r.HabitNodeId).ToList()).ConfigureAwait(true);
        if (generation != _generation) return;
        await Task.WhenAll(rows.Select(r => r.LoadKeyFigureAsync(
            boundaries.TryGetValue(r.HabitNodeId, out var b) ? b : appDay))).ConfigureAwait(true);
    }

    /// <summary>Il clic su una riga della colonna (le righe sono quelle della Home): apre la figlia.</summary>
    [RelayCommand]
    private void SelectRow(HomeRowViewModel row) => ChildSelected?.Invoke(this, row.HabitNodeId);

    [RelayCommand]
    private void SelectChild(PanoramicaChildRow row) => ChildSelected?.Invoke(this, row.HabitNodeId);
}

/// <summary>One descendant row in the group overview: a habit, or a subgroup heading its
/// own indented sub-block.</summary>
public sealed record PanoramicaChildRow(
    Guid HabitNodeId,
    string Name,
    string ColorHex,
    bool IsCompletedToday,
    ImmutableArray<int> CompletionsLast7Days,
    bool IsGroup = false,
    int Indent = 0,
    int ChildCount = 0);
