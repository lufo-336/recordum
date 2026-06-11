using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.Quick;

namespace Recordum.WPF.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
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

    partial void OnSelectedRowChanged(HomeRowViewModel? oldValue, HomeRowViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
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
        var keep = SelectedRow?.HabitNodeId;
        await LoadAsync();
        SelectedRow = keep is { } id ? Rows.FirstOrDefault(r => r.HabitNodeId == id) : null;
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
                Rows.Add(vm);
            }
        }
        finally { _rebuilding = false; }
        State = Rows.Count == 0 ? HomeState.Empty : HomeState.Loaded;
        foreach (var r in Rows) r.IsSelected = ReferenceEquals(r, SelectedRow);
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
