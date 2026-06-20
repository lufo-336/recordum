using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>One open tab in the workspace shell. Wraps a per-tab <see cref="HabitScreenViewModel"/>
/// (its panes/content) and the tab's identity + preview state. A "preview" tab is the transient,
/// italic, single-click tab that the next single open replaces; pinning makes it persistent.</summary>
public sealed partial class SubjectTabViewModel : ObservableObject, IDisposable
{
    public Guid SubjectId { get; }
    public WorkspaceTabKind Kind { get; }
    public HabitScreenViewModel Screen { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isPreview;

    private readonly IDisposable _scope;

    public SubjectTabViewModel(Guid subjectId, WorkspaceTabKind kind, string title,
        HabitScreenViewModel screen, IDisposable scope, bool isPreview)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(scope);
        SubjectId = subjectId;
        Kind = kind;
        Screen = screen;
        _scope = scope;
        _title = title;
        _isPreview = isPreview;
    }

    /// <summary>Promote a preview tab to a persistent one (no-op if already pinned).</summary>
    public void Pin() => IsPreview = false;

    /// <summary>Disposes the tab's DI scope, releasing its HabitScreenViewModel and scoped services.</summary>
    public void Dispose() => _scope.Dispose();
}
