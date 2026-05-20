using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IHabitTreeQueryService _query;
    private readonly IHabitNodeService _nodes;
    private readonly IPurgeService _purge;
    private IReadOnlyList<HomeTreeRow> _lastFetched = Array.Empty<HomeTreeRow>();
    private bool _suppressRebuild;

    [ObservableProperty] private HomeTab _activeTab = HomeTab.Active;
    [ObservableProperty] private HomeState _state = HomeState.Loading;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private ImmutableHashSet<HabitType> _typeFilter = ImmutableHashSet<HabitType>.Empty;
    [ObservableProperty] private ImmutableHashSet<Guid> _tagFilter = ImmutableHashSet<Guid>.Empty;
    [ObservableProperty] private bool _hideCompletedToday;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private HomeSort _sortOption = HomeSort.Manual;

    public ObservableCollection<HomeRowViewModel> Rows { get; } = new();

    public HomeViewModel(IHabitTreeQueryService query, IHabitNodeService nodes, IPurgeService purge)
    {
        _query = query;
        _nodes = nodes;
        _purge = purge;
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
        await LoadAsync();
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

    private void Rebuild()
    {
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
        foreach (var (row, level) in OrderHierarchically(q.ToList()))
            Rows.Add(new HomeRowViewModel(row, level, _nodes, _purge));
        State = Rows.Count == 0 ? HomeState.Empty : HomeState.Loaded;
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

    private IEnumerable<(HomeTreeRow Row, int Level)> WalkSubtree(HomeTreeRow node, int level, ILookup<Guid?, HomeTreeRow> byParent)
    {
        yield return (node, level);
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
