using System.Globalization;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Quick;

namespace Recordum.Presentation.ViewModels;

public sealed partial class HomeViewModel : ObservableObject, IRowSelectionHost, IDisposable
{
    private readonly IHabitTreeQueryService _query;
    private readonly IHabitNodeService _nodes;
    private readonly IPurgeService _purge;
    private readonly IHabitTemplatePickerLauncher? _launcher;
    private readonly QuickActionHandlerSelector? _quickActions;
    private readonly IHabitUiPrefsService? _uiPrefs;
    private readonly IStreakAggregator? _streaks;
    private readonly IClock? _clock;
    private readonly IResetTimeResolver? _resetTimes;
    private readonly HomeClockTicker _clockTicker;
    private Dictionary<Guid, HabitUiPrefs> _prefsByNode = new();
    private IReadOnlyList<HomeTreeRow> _lastFetched = Array.Empty<HomeTreeRow>();
    private bool _suppressRebuild;
    private readonly HashSet<Guid> _collapsedGroupIds = [];
    private bool _rebuilding;
    private readonly HashSet<Guid> _selectedIds = [];
    private Guid? _selectionAnchor;
    private bool _syncingSelection;

    [ObservableProperty] private HomeTab _activeTab = HomeTab.Active;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStripHeader))]
    private HomeState _state = HomeState.Loading;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private ImmutableHashSet<HabitType> _typeFilter = ImmutableHashSet<HabitType>.Empty;
    [ObservableProperty] private ImmutableHashSet<Guid> _tagFilter = ImmutableHashSet<Guid>.Empty;
    [ObservableProperty] private bool _hideCompletedToday;

    /// <summary>La data lunga del giorno dell'app accanto a "Oggi" ("mercoledì 28 ottobre"); vuota senza orologio.</summary>
    [ObservableProperty] private string _todayLabel = string.Empty;

    /// <summary>L'intestazione dei giorni sopra le strisce: 14 celle dal più vecchio, l'ultima è "oggi".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStripHeader))]
    private ImmutableArray<StripCell> _stripHeader = ImmutableArray<StripCell>.Empty;

    /// <summary>La striscia più lunga fra le righe visibili: quante celle dell'intestazione mostrare.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStripHeader))]
    private int _maxVisibleStripLength;

    /// <summary>L'intestazione dei giorni: solo con le righe caricate, una striscia e il giorno risolto.</summary>
    public bool ShowStripHeader => State == HomeState.Loaded && MaxVisibleStripLength > 0 && !StripHeader.IsDefaultOrEmpty;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private HomeSort _sortOption = HomeSort.Manual;

    [ObservableProperty] private HomeRowViewModel? _selectedRow;

    public ObservableCollection<HomeRowViewModel> Rows { get; } = new();

    /// <summary>Top-level rows for the hierarchical view (the frame around group children
    /// requires children to render INSIDE the group template). Built by Rebuild alongside
    /// the flat Rows, which stays the source of truth for selection/shift/drag.</summary>
    public ObservableCollection<HomeRowViewModel> Roots { get; } = new();

    public IReadOnlyList<HomeSort> AvailableSorts { get; } =
        new[] { HomeSort.Manual, HomeSort.Name, HomeSort.LastCompletion };

    public HomeViewModel(
        IHabitTreeQueryService query,
        IHabitNodeService nodes,
        IPurgeService purge,
        IHabitTemplatePickerLauncher? launcher = null,
        QuickActionHandlerSelector? quickActions = null,
        IEventBus? events = null,
        IHabitUiPrefsService? uiPrefs = null,
        IStreakAggregator? streaks = null,
        IClock? clock = null,
        IResetTimeResolver? resetTimes = null,
        ITickSourceFactory? ticks = null)
    {
        _query = query;
        _nodes = nodes;
        _purge = purge;
        _launcher = launcher;
        _quickActions = quickActions;
        _uiPrefs = uiPrefs;
        _streaks = streaks;
        _clock = clock;
        _resetTimes = resetTimes;
        _clockTicker = new HomeClockTicker(ticks);

        // Any completion change (quick-action, detail QuickAdd, retroactive, edit, delete) must
        // refresh the row indicators (sparkline / completed-today); a habit edit (name/color/icon
        // in Configura) must refresh the row's name and accent. The bus is the single signal source.
        // SubscribeOnUi marshals onto the UI dispatcher: the bus invokes handlers on the publisher's
        // thread (quick-action handlers record with ConfigureAwait(false)), so events can arrive off
        // the UI thread, and OnRowsInvalidated touches Rows (an ObservableCollection bound to the view).
        events?.SubscribeOnUi(WellKnownEvents.CompletionRecorded, OnRowsInvalidated);
        events?.SubscribeOnUi(WellKnownEvents.CompletionUpdated, OnRowsInvalidated);
        events?.SubscribeOnUi(WellKnownEvents.CompletionDeleted, OnRowsInvalidated);
        events?.SubscribeOnUi(WellKnownEvents.HabitUpdated, OnRowsInvalidated);
        // Abstinence (U9) records no completion, so none of the above ever fires for a resistance
        // habit: the row kept a live clock running on a period that had already closed (W2V-6).
        events?.SubscribeOnUi(WellKnownEvents.AbstinenceStarted, OnRowsInvalidated);
        events?.SubscribeOnUi(WellKnownEvents.AbstinenceLapsed, OnRowsInvalidated);
        events?.SubscribeOnUi(WellKnownEvents.AbstinencePeriodDeleted, OnRowsInvalidated);
    }

    private void OnRowsInvalidated(EventEnvelope env) => _ = ReloadPreservingSelectionAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        State = HomeState.Loading;
        ErrorMessage = null;
        try
        {
            _lastFetched = ActiveTab == HomeTab.Trash
                ? await _query.GetTrashTreeAsync()
                : await _query.GetHomeTreeAsync();
            await PrefetchPrefsAsync();
            Rebuild();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = HomeState.Error;
            Rows.Clear();
            return;
        }
        // Fuori dal try: l'intestazione è un di più, e un confine del giorno che non si legge non spegne la Home (review M-6).
        try { await RefreshHeaderAsync(); }
        catch (Exception) { StripHeader = ImmutableArray<StripCell>.Empty; TodayLabel = string.Empty; }
    }

    [RelayCommand]
    private async Task SwitchTabAsync(HomeTab tab)
    {
        if (ActiveTab == tab) return;
        ActiveTab = tab;
        ResetFilters();
        SelectedRow = null;
        await LoadAsync();
    }

    [RelayCommand]
    private void SelectRow(HomeRowViewModel? row) => SelectedRow = row;

    public bool HasMultiSelection => _selectedIds.Count > 1;

    /// <summary>File-manager selection (spec §C.1): plain=single, Ctrl=toggle, Shift=range from anchor.</summary>
    public void SelectInteractive(HomeRowViewModel row, bool ctrl, bool shift)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (shift && _selectionAnchor is { } anchorId
                  && Rows.FirstOrDefault(r => r.HabitNodeId == anchorId) is { } anchorRow)
        {
            var from = Rows.IndexOf(anchorRow);
            var to = Rows.IndexOf(row);
            _selectedIds.Clear();
            foreach (var r in Rows.Skip(Math.Min(from, to)).Take(Math.Abs(to - from) + 1))
                _selectedIds.Add(r.HabitNodeId);
        }
        else if (ctrl)
        {
            if (!_selectedIds.Remove(row.HabitNodeId)) _selectedIds.Add(row.HabitNodeId);
            _selectionAnchor = row.HabitNodeId;
        }
        else
        {
            _selectedIds.Clear();
            _selectedIds.Add(row.HabitNodeId);
            _selectionAnchor = row.HabitNodeId;
        }
        ApplySelectionToRows();
        SyncSelectedRowFromSet();
    }

    /// <summary>Esc with 2+ selected, or tab switches: back to no selection (spec §C.1).</summary>
    public void ClearSelection()
    {
        _selectedIds.Clear();
        _selectionAnchor = null;
        ApplySelectionToRows();
        SyncSelectedRowFromSet();
    }

    private void ApplySelectionToRows()
    {
        foreach (var r in Rows) r.IsSelected = _selectedIds.Contains(r.HabitNodeId);
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    // SelectedRow is the single-selection API the workspace tracks: non-null only when
    // exactly one row is selected. The guard stops the setter's partial from clobbering the set.
    private void SyncSelectedRowFromSet()
    {
        _syncingSelection = true;
        try
        {
            SelectedRow = _selectedIds.Count == 1
                ? Rows.FirstOrDefault(r => _selectedIds.Contains(r.HabitNodeId))
                : null;
        }
        finally { _syncingSelection = false; }
    }

    public async Task<bool> TryBulkAsync(HomeRowViewModel origin, RowBulkAction action)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!HasMultiSelection || !origin.IsSelected) return false;
        var ids = Rows.Where(r => r.IsSelected).Select(r => r.HabitNodeId).ToList();
        foreach (var id in ids)
        {
            switch (action)
            {
                case RowBulkAction.Archive: await _nodes.SetArchivedAsync(id, true); break;
                case RowBulkAction.Unarchive: await _nodes.SetArchivedAsync(id, false); break;
                case RowBulkAction.SoftDelete: await _nodes.SoftDeleteAsync(id); break;
                case RowBulkAction.Restore: await _nodes.RestoreAsync(id); break;
                case RowBulkAction.Purge: await _purge.PurgeSubtreeAsync(id); break;
            }
        }
        ClearSelection();
        await LoadAsync();
        return true;
    }

    /// <summary>Bulk move from drag-drop: moves every selected row, skipping cyclic targets (spec §C.3).</summary>
    public async Task<bool> TryBulkMoveAsync(HomeRowViewModel origin, Guid? newParentId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!HasMultiSelection || !origin.IsSelected) return false;
        var ids = Rows.Where(r => r.IsSelected).Select(r => r.HabitNodeId).ToList();
        foreach (var id in ids)
        {
            try { await _nodes.MoveAsync(id, newParentId); }
            catch (Exception ex) when (ex is CyclicHabitNodeMoveException or HabitNodeNotFoundException or HabitNodeAlreadySoftDeletedException)
            { /* skip this row: cyclic, or the node/parent vanished from a concurrent reload/cleanup (CR16) */ }
        }
        ClearSelection();
        await LoadAsync();
        return true;
    }

    /// <summary>
    /// Applies a resolved drop: bulk selection → reparent in blocco (existing semantics);
    /// single row → MoveAndReorderAsync. A fine-grained reorder (anchor != null) forces
    /// Manual sort so the new order is visible and sticks.
    /// </summary>
    public async Task ApplyDropAsync(HomeRowViewModel src, DropPlan plan)
    {
        if (await TryBulkMoveAsync(src, plan.Parent))
            return; // bulk path reparents and reloads by itself

        try
        {
            await _nodes.MoveAndReorderAsync(src.HabitNodeId, plan.Parent, plan.Anchor, plan.PlaceAfter);
        }
        catch (Exception ex) when (ex is CyclicHabitNodeMoveException or HabitNodeNotFoundException or HabitNodeAlreadySoftDeletedException)
        {
            // Cyclic move, or the dragged node / target parent was removed by a concurrent reload or
            // cleanup between the drag and the drop — the drop is a no-op instead of crashing the
            // process out of the async-void OnDrop handler (CR16).
            return;
        }

        // A fine-grained reorder (anchor != null) forces Manual sort so the new order is visible.
        // We suppress the setter's Rebuild because it would sort the *stale* _lastFetched snapshot
        // (MoveAndReorderAsync wrote the new SortOrder to the DB, not to that cache); the reload
        // below re-fetches so the fresh order actually shows. Onto-group append just reloads.
        if (plan.Anchor is not null && SortOption != HomeSort.Manual)
        {
            _suppressRebuild = true;
            SortOption = HomeSort.Manual;
            _suppressRebuild = false;
        }
        await LoadCommand.ExecuteAsync(null);
    }

    /// <summary>Selects a node by id, expanding any collapsed ancestor so its row is visible
    /// (used by the group Panoramica child navigation, spec §A.3).</summary>
    public void SelectById(Guid id)
    {
        var byId = _lastFetched.ToDictionary(r => r.HabitNodeId);
        var changed = false;
        for (var cur = byId.GetValueOrDefault(id); cur?.ParentId is { } pid; cur = byId.GetValueOrDefault(pid))
            changed |= _collapsedGroupIds.Remove(pid);
        if (changed) Rebuild();
        SelectedRow = Rows.FirstOrDefault(r => r.HabitNodeId == id);
    }

    partial void OnSelectedRowChanged(HomeRowViewModel? oldValue, HomeRowViewModel? newValue)
    {
        if (!_syncingSelection)
        {
            // Direct assignment (tree click plumbing, SelectById, deselects) = single selection.
            _selectedIds.Clear();
            if (newValue is not null)
            {
                _selectedIds.Add(newValue.HabitNodeId);
                _selectionAnchor = newValue.HabitNodeId;
            }
        }
        ApplySelectionToRows();
    }

    [RelayCommand]
    private async Task OpenNewHabitAsync()
    {
        if (_launcher is null) return;
        var newId = await _launcher.PickAsync(ResolveCreationParentId());
        if (newId is null) return;
        ++_reloadGeneration;   // un ricaricamento partito con il selettore aperto porta la vecchia selezione: qui vince la nuova
        await LoadAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.HabitNodeId == newId);
    }

    // ＋Nuova▾ context: group selected → inside it; habit selected → sibling; nothing → root.
    private Guid? ResolveCreationParentId() => SelectedRow switch
    {
        null => null,
        { IsGroup: true } row => row.HabitNodeId,
        var row => row.ParentId,
    };

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        var id = await _nodes.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Group,
            Name = Strings.Editor_Default_NewGroup,
            Color = "#808080",
            ParentId = ResolveCreationParentId(),
        });
        await LoadAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.HabitNodeId == id);
        GroupCreated?.Invoke(this, id);
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private void ToggleTypeFilter(HabitType type) =>
        TypeFilter = TypeFilter.SymmetricExcept(new[] { type });

    partial void OnTypeFilterChanged(ImmutableHashSet<HabitType> value) { if (!_suppressRebuild) Rebuild(); }
    partial void OnTagFilterChanged(ImmutableHashSet<Guid> value) { if (!_suppressRebuild) Rebuild(); }
    partial void OnHideCompletedTodayChanged(bool value) { if (!_suppressRebuild) Rebuild(); }
    partial void OnSearchTextChanged(string? value) { if (!_suppressRebuild) Rebuild(); }
    partial void OnSortOptionChanged(HomeSort value) { if (!_suppressRebuild) Rebuild(); }

    private void ResetFilters()
    {
        _suppressRebuild = true;
        try
        {
            SearchText = null;
            TypeFilter = ImmutableHashSet<HabitType>.Empty;
            TagFilter = ImmutableHashSet<Guid>.Empty;
            HideCompletedToday = false;
            SortOption = HomeSort.Manual;
        }
        finally
        {
            _suppressRebuild = false;
        }
    }

    private int _reloadGeneration;
    private async Task ReloadPreservingSelectionAsync()
    {
        // Overlapping bus events (completion/note/habit) fire fire-and-forget reloads. Without a guard
        // two reloads interleave Rebuild + selection on the same Rows; the stale one must not re-apply
        // its selection over the newer reload's result (CR18 — HabitScreen/Detail already do this).
        var generation = ++_reloadGeneration;
        var keep = _selectedIds.ToList();
        await LoadAsync();
        if (generation != _reloadGeneration) return;
        _selectedIds.Clear();
        foreach (var id in keep.Where(id => Rows.Any(r => r.HabitNodeId == id)))
            _selectedIds.Add(id);
        ApplySelectionToRows();
        SyncSelectedRowFromSet();
    }

    // A row mutated its membership/position (delete/archive/restore/purge/move/pin).
    private async void OnRowChanged(object? sender, EventArgs e) => await ReloadPreservingSelectionAsync();

    /// <summary>Raised when the user requests recording on a habit whose row is already selected.
    /// Setting <see cref="SelectedRow"/> would be a no-op, so the host must (re)open the workspace
    /// explicitly. Carries the habit node id to open.</summary>
    public event EventHandler<Guid>? HabitActivationRequested;

    /// <summary>Raised after a group is created via the ＋New menu (host opens Configura for rename).</summary>
    public event EventHandler<Guid>? GroupCreated;

    /// <summary>Empty-space click in the tree: ask the host to return to the full-width Home (Model A).</summary>
    public event EventHandler? BackgroundClicked;

    /// <summary>Empty-space click in the tree (Model A): deselect any row AND signal a return to the
    /// full-width Home. Fires even when no row is selected — tabs are independent of the tree selection,
    /// so a tab can be active with <see cref="SelectedRow"/> already null (re-activated from the strip).</summary>
    public void ClickBackground()
    {
        SelectedRow = null;
        BackgroundClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnRowQuickActionExecuted(object? sender, QuickActionExecutedEventArgs e)
    {
        // recorded == true: the completion fires a bus event that refreshes the list
        // (see OnCompletionEvent) — nothing to do here.
        // recorded == false: one-click recording doesn't apply (State/Resistance/Journal),
        // so open the detail and let the user record the value via the QuickAdd panel.
        if (e.Recorded || sender is not HomeRowViewModel row) return;
        if (SelectedRow == row)
            // already selected → the SelectedRow setter won't notify; ask the host to (re)open.
            HabitActivationRequested?.Invoke(this, row.HabitNodeId);
        else
            SelectedRow = row;
    }

    private void Rebuild()
    {
        foreach (var old in Rows)
        {
            old.Changed -= OnRowChanged;
            old.QuickActionExecuted -= OnRowQuickActionExecuted;
        }
        Rows.Clear();
        Roots.Clear();
        IEnumerable<HomeTreeRow> q = _lastFetched;
        q = ActiveTab switch
        {
            HomeTab.Active => q.Where(r => !r.IsArchived),
            HomeTab.Archived => q.Where(r => r.IsArchived),
            _ => q,
        };
        if (!TypeFilter.IsEmpty)
            q = q.Where(r => r.HabitType.HasValue && TypeFilter.Contains(r.HabitType.Value));
        if (!TagFilter.IsEmpty)
            q = q.Where(r => r.TagIds.Any(TagFilter.Contains));
        if (HideCompletedToday && ActiveTab == HomeTab.Active)
            q = q.Where(r => !r.IsCompletedToday);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var needle = SearchText.Trim();
            q = q.Where(r => r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }
        _rebuilding = true;
        _clockTicker.Clear();
        try
        {
            var parentStack = new List<HomeRowViewModel>();
            var ordered = OrderHierarchically(q.ToList()).ToList();
            var frames = FrameSlicesOf(ordered);
            for (var i = 0; i < ordered.Count; i++)
            {
                var (row, level) = ordered[i];
                _prefsByNode.TryGetValue(row.HabitNodeId, out var prefs);
                var vm = new HomeRowViewModel(row, level, _nodes, _purge, prefs, _streaks, _clock)
                {
                    FrameSlices = frames[i],
                    CurrentTab = ActiveTab,
                    IsExpanded = !_collapsedGroupIds.Contains(row.HabitNodeId),
                };
                if (_quickActions is not null) vm.AttachQuickActionSelector(_quickActions);
                if (vm.IsResistanceClock) _clockTicker.Register(vm.RefreshClock);
                vm.Changed += OnRowChanged;
                vm.QuickActionExecuted += OnRowQuickActionExecuted;
                vm.ExpandToggled += OnRowExpandToggled;
                vm.AttachSelectionHost(this);
                Rows.Add(vm);

                // Hierarchy mirror: level 0 → Roots, deeper rows → parent's Children.
                while (parentStack.Count > level) parentStack.RemoveAt(parentStack.Count - 1);
                if (level == 0) Roots.Add(vm);
                else parentStack[level - 1].Children.Add(vm);
                if (parentStack.Count == level) parentStack.Add(vm);
            }
        }
        finally { _rebuilding = false; }
        ApplyGroupProgress();
        MaxVisibleStripLength = Rows.Count == 0 ? 0 : Rows.Max(r => r.StripCells.Length);
        State = Rows.Count == 0 ? HomeState.Empty : HomeState.Loaded;
        ApplySelectionToRows();
        _ = FillKeyFiguresAsync();
    }

    /// <summary>"3 di 5" dei gruppi: le abitudini discendenti del gruppo fra quelle non archiviate, non solo le righe
    /// visibili, così il filtro "fatte" non cambia il conto.</summary>
    private void ApplyGroupProgress()
    {
        var live = _lastFetched.Where(r => !r.IsArchived).ToList();
        var byParent = live.ToLookup(r => r.ParentId);
        foreach (var vm in Rows.Where(r => r.IsGroup))
        {
            var (done, total) = GroupProgress.Count(byParent, vm.HabitNodeId);
            vm.SetGroupProgress(done, total);
        }
    }

    /// <summary>La testa della Home: la data lunga e i 14 giorni dell'intestazione, dal giorno dell'app
    /// (<c>daily_reset_time</c>) risolto una volta. Senza orologio resta vuota (superfici di design, test).</summary>
    private async Task RefreshHeaderAsync()
    {
        if (_clock is null) return;
        var appDay = _resetTimes is null
            ? DayBoundary.Utc
            : await _resetTimes.GetAppDayBoundaryAsync().ConfigureAwait(true);
        var today = appDay.Today(_clock);
        var culture = CultureInfo.CurrentCulture;
        var date = today.ToDateTime(TimeOnly.MinValue);
        TodayLabel = $"{date.ToString("dddd", culture)} {date.ToString(culture.DateTimeFormat.MonthDayPattern, culture)}";
        var cells = ImmutableArray.CreateBuilder<StripCell>(14);
        for (var back = 13; back >= 0; back--)
        {
            var day = today.AddDays(-back).ToDateTime(TimeOnly.MinValue);
            cells.Add(back == 0
                ? new StripCell(Resources.Strings.Home_StripHeader_Today, false, true)
                : new StripCell(day.ToString("ddd", culture), false, false));
        }
        StripHeader = cells.MoveToImmutable();
    }

    /// <summary>Loads per-node presentation prefs for the current fetch, once, into a lookup the
    /// synchronous Rebuild reads. No-op when no prefs service is wired (design surfaces, tests).</summary>
    private async Task PrefetchPrefsAsync()
    {
        if (_uiPrefs is null) { _prefsByNode = new(); return; }
        var ids = _lastFetched.Where(r => r.HabitType is not null)
            .Select(r => r.HabitNodeId).Distinct().ToList();
        var pairs = await Task.WhenAll(ids.Select(async id => (id, prefs: await _uiPrefs.GetAsync(id))));
        _prefsByNode = pairs.ToDictionary(p => p.id, p => p.prefs);
    }

    /// <summary>Fills the off-critical-path key figures (streak) for the current rows in parallel,
    /// after render. Mirrors the CR24 Task.WhenAll async-fill pattern; never blocks the initial build.</summary>
    private async Task FillKeyFiguresAsync()
    {
        if (_streaks is null || _clock is null) return;
        var rows = Rows.Where(r => r.KeyFigureKind == KeyFigureKind.Streak).ToList();
        if (rows.Count == 0) return;

        // L'unico punto WPF che carica N abitudini insieme: il confine si risolve in BATCH. Con la
        // versione singola qui tornerebbero N cascate reset_time, una per riga.
        // Senza resolver il confine e DayBoundary.Utc, cioe l identita: e il comportamento pre-R9,
        // e tiene in piedi i test che costruiscono la Home senza quel servizio.
        var appDay = _resetTimes is null
            ? DayBoundary.Utc
            : await _resetTimes.GetAppDayBoundaryAsync().ConfigureAwait(true);
        var boundaries = _resetTimes is null
            ? new Dictionary<Guid, DayBoundary>()
            : (IReadOnlyDictionary<Guid, DayBoundary>)await _resetTimes
                .GetDayBoundariesAsync(rows.Select(r => r.HabitNodeId).ToList())
                .ConfigureAwait(true);
        await Task.WhenAll(rows.Select(r => r.LoadKeyFigureAsync(
            boundaries.TryGetValue(r.HabitNodeId, out var b) ? b : appDay)));
    }

    private IEnumerable<(HomeTreeRow Row, int Level)> OrderHierarchically(IReadOnlyList<HomeTreeRow> rows)
    {
        if (ActiveTab != HomeTab.Active)
        {
            foreach (var r in ApplySort(rows)) yield return (r, 0);
            yield break;
        }

        var byParent = rows.ToLookup(r => r.ParentId);
        var roots = ApplySort(byParent[null].ToList())
            .OrderByDescending(r => r.PinScope == PinScope.Global)
            .ThenByDescending(r => r.PinScope == PinScope.Local)
            .ToList();

        foreach (var root in roots)
            foreach (var pair in WalkSubtree(root, 0, byParent))
                yield return pair;
    }

    /// <summary>For each row of the display order, the frames of the expanded groups that cross it.
    /// A group has a frame when the next row is one of its descendants (collapsed groups have none:
    /// their children are not in the order); the frame ends at the last row before the next row at
    /// its level or above.</summary>
    private static IReadOnlyList<GroupFrameSlice>[] FrameSlicesOf(List<(HomeTreeRow Row, int Level)> ordered)
    {
        var result = new IReadOnlyList<GroupFrameSlice>[ordered.Count];
        var open = new List<(int Level, string ColorHex, int Start)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var (row, level) = ordered[i];
            while (open.Count > 0 && open[^1].Level >= level) open.RemoveAt(open.Count - 1);
            var nextLevel = i + 1 < ordered.Count ? ordered[i + 1].Level : 0;
            if (row.HabitType is null && nextLevel > level) open.Add((level, row.ColorHex, i));
            result[i] = open.Count == 0
                ? []
                : open.Select(f => new GroupFrameSlice(f.Level, f.ColorHex, f.Start == i, f.Level >= nextLevel)).ToArray();
        }
        return result;
    }

    // ▾/▸ toggle: track the collapsed set and rebuild the visible rows. The guard skips
    // the notifications fired by the IsExpanded initializer while Rebuild constructs rows.
    private void OnRowExpandToggled(object? sender, EventArgs e)
    {
        if (_rebuilding || sender is not HomeRowViewModel row || !row.IsGroup) return;
        if (row.IsExpanded) _collapsedGroupIds.Remove(row.HabitNodeId);
        else _collapsedGroupIds.Add(row.HabitNodeId);
        Rebuild();
    }

    private IEnumerable<(HomeTreeRow Row, int Level)> WalkSubtree(HomeTreeRow node, int level, ILookup<Guid?, HomeTreeRow> byParent)
    {
        yield return (node, level);
        if (_collapsedGroupIds.Contains(node.HabitNodeId)) yield break;
        var children = byParent[node.HabitNodeId].ToList();
        var sortedChildren = ApplySort(children)
            .OrderByDescending(c => c.PinScope == PinScope.Local)
            .ToList();
        foreach (var c in sortedChildren)
            foreach (var pair in WalkSubtree(c, level + 1, byParent))
                yield return pair;
    }

    private IEnumerable<HomeTreeRow> ApplySort(IReadOnlyList<HomeTreeRow> rows) => SortOption switch
    {
        HomeSort.Name => rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        HomeSort.LastCompletion => rows.OrderByDescending(r => r.LastCompletionAt ?? DateTime.MinValue),
        HomeSort.Manual => rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => rows,
    };

    /// <summary>Stops the shared Home clock ticker so it stops ticking and can be collected.</summary>
    public void Dispose() => _clockTicker.Dispose();
}
