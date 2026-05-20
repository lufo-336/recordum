using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels;

public sealed partial class HomeRowViewModel : ObservableObject
{
    private readonly IHabitNodeService _nodes;
    private readonly IPurgeService _purge;
    private string _originalName = string.Empty;

    public Guid HabitNodeId { get; }
    public Guid? ParentId { get; }
    public string ColorHex { get; }
    public HabitType? HabitType { get; }
    public bool IsGroup => HabitType is null;
    public int ChildCount { get; }
    public ImmutableArray<int> CompletionsLast7Days { get; }
    public DateTime? LastCompletionAt { get; }
    public bool IsCompletedToday { get; }
    public int IndentLevel { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private PinScope _pinScope;
    [ObservableProperty] private bool _isArchived;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _pendingName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRenameMenu))]
    [NotifyPropertyChangedFor(nameof(ShowPinMenu))]
    [NotifyPropertyChangedFor(nameof(ShowArchive))]
    [NotifyPropertyChangedFor(nameof(ShowUnarchive))]
    [NotifyPropertyChangedFor(nameof(ShowSoftDelete))]
    [NotifyPropertyChangedFor(nameof(ShowRestore))]
    [NotifyPropertyChangedFor(nameof(ShowPurge))]
    private HomeTab _currentTab = HomeTab.Active;

    public bool ShowRenameMenu => CurrentTab != HomeTab.Trash;
    public bool ShowPinMenu    => CurrentTab == HomeTab.Active && !IsGroup;
    public bool ShowArchive    => CurrentTab == HomeTab.Active;
    public bool ShowUnarchive  => CurrentTab == HomeTab.Archived;
    public bool ShowSoftDelete => CurrentTab is HomeTab.Active or HomeTab.Archived;
    public bool ShowRestore    => CurrentTab == HomeTab.Trash;
    public bool ShowPurge      => CurrentTab == HomeTab.Trash;

    public HomeRowViewModel(HomeTreeRow row, int indentLevel, IHabitNodeService nodes, IPurgeService purge)
    {
        _nodes = nodes;
        _purge = purge;
        HabitNodeId = row.HabitNodeId;
        ParentId = row.ParentId;
        ColorHex = row.ColorHex;
        HabitType = row.HabitType;
        ChildCount = row.ChildCount;
        CompletionsLast7Days = row.CompletionsLast7Days;
        LastCompletionAt = row.LastCompletionAt;
        IsCompletedToday = row.IsCompletedToday;
        IndentLevel = indentLevel;
        _name = row.Name;
        _originalName = row.Name;
        _pinScope = row.PinScope;
        _isArchived = row.IsArchived;
    }

    [RelayCommand]
    private void BeginRename()
    {
        PendingName = Name;
        IsRenaming = true;
    }

    [RelayCommand]
    private async Task CommitRenameAsync()
    {
        if (!IsRenaming) return;
        var newName = PendingName?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == _originalName)
        {
            IsRenaming = false;
            return;
        }
        try
        {
            await _nodes.UpdateAsync(HabitNodeId, n => n.Name = newName);
            _originalName = newName;
            Name = newName;
        }
        catch
        {
            Name = _originalName;
        }
        finally
        {
            IsRenaming = false;
        }
    }

    [RelayCommand]
    private void CancelRename()
    {
        IsRenaming = false;
        PendingName = string.Empty;
    }

    [RelayCommand]
    private async Task SetPinScopeAsync(PinScope scope)
    {
        if (PinScope == scope) return;
        await _nodes.SetPinScopeAsync(HabitNodeId, scope);
        PinScope = scope;
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    {
        await _nodes.SetArchivedAsync(HabitNodeId, true);
        IsArchived = true;
    }

    [RelayCommand]
    private async Task UnarchiveAsync()
    {
        await _nodes.SetArchivedAsync(HabitNodeId, false);
        IsArchived = false;
    }

    [RelayCommand]
    private Task SoftDeleteAsync() => _nodes.SoftDeleteAsync(HabitNodeId).AsTask();

    [RelayCommand]
    private Task RestoreAsync() => _nodes.RestoreAsync(HabitNodeId).AsTask();

    [RelayCommand]
    private Task PurgeAsync() => _purge.PurgeSubtreeAsync(HabitNodeId).AsTask();

    public Task MoveToAsync(Guid? newParent) => _nodes.MoveAsync(HabitNodeId, newParent).AsTask();
}
