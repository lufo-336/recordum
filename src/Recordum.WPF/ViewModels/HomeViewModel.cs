using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.Quick;

namespace Recordum.WPF.ViewModels;

public sealed partial class HomeViewModel : ObservableObject, IRowSelectionHost
{
    private readonly IHabitTreeQueryService _query;
    private readonly IHabitNodeService _nodes;
    private readonly IPurgeService _purge;
    private readonly IHabitTemplatePickerLauncher? _launcher;
    private readonly QuickActionHandlerSelector? _quickActions;
    private IReadOnlyList<HomeTreeRow> _lastFetched = Array.Empty<HomeTreeRow>();
    private bool _suppressRebuild;
    private readonly HashSet<Guid> _collapsedGroupIds = [];
    private bool _rebuilding;
    private readonly HashSet<Guid> _selectedIds = [];
    private Guid? _selectionAnchor;
    private bool _syncingSelection;

    [ObservableProperty] private HomeTab _activeTab = HomeTab.Active;
    [ObservableProperty] private HomeState _state = HomeState.Loading;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private ImmutableHashSet<HabitType> _typeFilter = ImmutableHashSet<HabitType>.Empty;
    [ObservableProperty] private ImmutableHashSet<Guid> _tagFilter = ImmutableHashSet<Guid>.Empty;
    [ObservableProperty] private bool _hideCompletedToday;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private HomeSort _sortOption = HomeSort.Manual;

    [ObservableProperty] private HomeRowViewModel? _selectedRow;

    public ObservableCollection<HomeRowViewModel> Rows { get; } = new();

    public IReadOnlyList<HomeSort> AvailableSorts { get; } =
        new[] { HomeSort.Manual, HomeSort.Name, HomeSort.LastCompletion };

    public HomeViewModel(
        IHabitTreeQueryService query,
        IHabitNodeService nodes,
        IPurgeService purge,
        IHabitTemplatePickerLauncher? launcher = null,
        QuickActionHandlerSelector? quickActions = null,
        IEventBus? events = null)
    {
        _query = query;
        _nodes = nodes;
        _purge = purge;
        _launcher = launcher;
        _quickActions = quickActions;

        // Any completion change (quick-action, detail QuickAdd, retroactive, edit, delete) must
        // refresh the row indicators (sparkline / completed-today); a habit edit (name/color/icon
        // in Configura) must refresh the row's name and accent. The bus is the single signal source.
        events?.Subscribe(WellKnownEvents.CompletionRecorded, OnRowsInvalidated);
        events?.Subscribe(WellKnownEvents.CompletionUpdated, OnRowsInvalidated);
        events?.Subscribe(WellKnownEvents.CompletionDeleted, OnRowsInvalidated);
        events?.Subscribe(WellKnownEvents.HabitUpdated, OnRowsInvalidated);
    }

    private void OnRowsInvalidated(EventEnvelope env)
    {
        // EventBus invokes handlers on the publisher's thread; quick-action handlers
        // record with ConfigureAwait(false), so this can arrive off the UI thread.
        // Marshal before touching Rows (an ObservableCollection bound to the view).
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            _ = ReloadPreservingSelectionAsync();
        else
            dispatcher.InvokeAsync(() => ReloadPreservingSelectionAsync());
    }

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
            Rebuild();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = HomeState.Error;
            Rows.Clear();
        }
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
            catch (CyclicHabitNodeMoveException) { /* skip, like the single-row path */ }
        }
        ClearSelection();
        await LoadAsync();
        return true;
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

    private async Task ReloadPreservingSelectionAsync()
    {
        var keep = _selectedIds.ToList();
        await LoadAsync();
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
        try
        {
            foreach (var (row, level) in OrderHierarchically(q.ToList()))
            {
                var vm = new HomeRowViewModel(row, level, _nodes, _purge)
                {
                    CurrentTab = ActiveTab,
                    IsExpanded = !_collapsedGroupIds.Contains(row.HabitNodeId),
                };
                if (_quickActions is not null) vm.AttachQuickActionSelector(_quickActions);
                vm.Changed += OnRowChanged;
                vm.QuickActionExecuted += OnRowQuickActionExecuted;
                vm.ExpandToggled += OnRowExpandToggled;
                vm.AttachSelectionHost(this);
                Rows.Add(vm);
            }
        }
        finally { _rebuilding = false; }
        State = Rows.Count == 0 ? HomeState.Empty : HomeState.Loaded;
        ApplySelectionToRows();
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
        _ => rows,
    };
}
