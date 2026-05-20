using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.WPF.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IHabitTreeQueryService _query;
    private readonly IHabitNodeService _nodes;
    private readonly IPurgeService _purge;
    private IReadOnlyList<HomeTreeRow> _lastFetched = Array.Empty<HomeTreeRow>();

    [ObservableProperty] private HomeTab _activeTab = HomeTab.Active;
    [ObservableProperty] private HomeState _state = HomeState.Loading;
    [ObservableProperty] private string? _errorMessage;

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
            State = Rows.Count == 0 ? HomeState.Empty : HomeState.Loaded;
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
        await LoadAsync();
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    private void Rebuild()
    {
        Rows.Clear();
        var filtered = ActiveTab switch
        {
            HomeTab.Active => _lastFetched.Where(r => !r.IsArchived),
            HomeTab.Archived => _lastFetched.Where(r => r.IsArchived),
            _ => _lastFetched,
        };
        foreach (var (row, level) in OrderHierarchically(filtered.ToList()))
            Rows.Add(new HomeRowViewModel(row, level, _nodes, _purge));
    }

    private static IEnumerable<(HomeTreeRow Row, int Level)> OrderHierarchically(IReadOnlyList<HomeTreeRow> rows)
    {
        foreach (var r in rows) yield return (r, 0);
    }
}
