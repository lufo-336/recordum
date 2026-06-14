using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.ViewModels.Quick;

namespace Recordum.WPF.ViewModels;

public sealed partial class HomeRowViewModel : ObservableObject
{
    private readonly IHabitNodeService _nodes;
    private readonly IPurgeService _purge;
    private QuickActionHandlerSelector? _quickActionSelector;
    private string _originalName = string.Empty;

    /// <summary>
    /// Raised after an operation that changes this row's membership or position
    /// in the tree (soft-delete, archive, restore, purge, move, pin). The Home
    /// list listens and reloads so the change is reflected without a tab switch.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the ▾/▸ toggle changes (HomeViewModel rebuilds the visible rows).</summary>
    public event EventHandler? ExpandToggled;

    partial void OnIsExpandedChanged(bool value) => ExpandToggled?.Invoke(this, EventArgs.Empty);

    public bool CanQuickAction => !IsGroup && HabitType is not null && _quickActionSelector is not null;

    private IRowSelectionHost? _selectionHost;
    public void AttachSelectionHost(IRowSelectionHost host) => _selectionHost = host;

    // Row actions go bulk when this row is part of a multi-selection (spec §C.3): the host
    // runs the op over every selected row with a single reload; otherwise fall through.
    private async Task<bool> HandledAsBulkAsync(RowBulkAction action)
        => _selectionHost is not null && await _selectionHost.TryBulkAsync(this, action);

    public void AttachQuickActionSelector(QuickActionHandlerSelector selector)
    {
        _quickActionSelector = selector;
        OnPropertyChanged(nameof(CanQuickAction));
        QuickActionCommand.NotifyCanExecuteChanged();
    }

    public Guid HabitNodeId { get; }
    public Guid? ParentId { get; }
    public string ColorHex { get; }
    /// <summary>Lucide icon resource key (e.g. "Icon.Heart"), or null/free-text for no icon.</summary>
    public string? Icon { get; }
    public HabitType? HabitType { get; }
    public bool IsGroup => HabitType is null;
    public int ChildCount { get; }
    public ImmutableArray<int> CompletionsLast7Days { get; }

    /// <summary>Visible children, populated by HomeViewModel.Rebuild (hierarchical view rendering).
    /// Empty for habits and for collapsed groups.</summary>
    public ObservableCollection<HomeRowViewModel> Children { get; } = new();

    /// <summary>True when expanded children are actually rendered (drives the group frame chrome).</summary>
    public bool HasVisibleChildren => Children.Count > 0;
    public DateTime? LastCompletionAt { get; }
    public bool IsCompletedToday { get; }
    public int IndentLevel { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private PinScope _pinScope;
    [ObservableProperty] private bool _isArchived;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _pendingName = string.Empty;
    [ObservableProperty] private bool _isSelected;

    /// <summary>True while a dragged row hovers this group's "into" zone (drop highlight).</summary>
    [ObservableProperty] private bool _isDropTarget;

    /// <summary>True for the row currently being dragged (drives the lifted/ghosted look).</summary>
    [ObservableProperty] private bool _isBeingDragged;

    /// <summary>Insertion indicator edge while a drag hovers this row (reorder affordance).</summary>
    [ObservableProperty] private RowDropEdge _dropEdge;

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
        Icon = row.Icon;
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
        // Children are filled by Rebuild right after construction; keep the frame chrome in sync.
        Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasVisibleChildren));
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
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    {
        if (await HandledAsBulkAsync(RowBulkAction.Archive)) return;
        await _nodes.SetArchivedAsync(HabitNodeId, true);
        IsArchived = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task UnarchiveAsync()
    {
        if (await HandledAsBulkAsync(RowBulkAction.Unarchive)) return;
        await _nodes.SetArchivedAsync(HabitNodeId, false);
        IsArchived = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task SoftDeleteAsync()
    {
        if (await HandledAsBulkAsync(RowBulkAction.SoftDelete)) return;
        await _nodes.SoftDeleteAsync(HabitNodeId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (await HandledAsBulkAsync(RowBulkAction.Restore)) return;
        await _nodes.RestoreAsync(HabitNodeId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task PurgeAsync()
    {
        if (await HandledAsBulkAsync(RowBulkAction.Purge)) return;
        await _purge.PurgeSubtreeAsync(HabitNodeId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<QuickActionExecutedEventArgs>? QuickActionExecuted;

    [RelayCommand(CanExecute = nameof(CanQuickAction))]
    private async Task QuickActionAsync(CancellationToken ct)
    {
        if (_quickActionSelector is null || HabitType is null) return;
        var handler = _quickActionSelector.For(HabitType.Value);
        if (handler is null) return;
        var recorded = await handler.ExecuteAsync(HabitNodeId, ct).ConfigureAwait(true);
        QuickActionExecuted?.Invoke(this, new QuickActionExecutedEventArgs(HabitNodeId, recorded));
    }

    public async Task MoveToAsync(Guid? newParent)
    {
        await _nodes.MoveAsync(HabitNodeId, newParent);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class QuickActionExecutedEventArgs(Guid habitNodeId, bool recorded) : EventArgs
{
    public Guid HabitNodeId { get; } = habitNodeId;
    public bool Recorded { get; } = recorded;
}
