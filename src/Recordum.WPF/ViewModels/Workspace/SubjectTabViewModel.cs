using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>One open tab in the workspace shell. Wraps a per-tab <see cref="HabitScreenViewModel"/>
/// (its panes/content) and the tab's identity + preview state. A "preview" tab is the transient,
/// italic, single-click tab that the next single open replaces; pinning makes it persistent.</summary>
public sealed partial class SubjectTabViewModel : ObservableObject
{
    public Guid SubjectId { get; }
    public WorkspaceTabKind Kind { get; }
    public HabitScreenViewModel Screen { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isPreview;

    public SubjectTabViewModel(Guid subjectId, WorkspaceTabKind kind, string title,
        HabitScreenViewModel screen, bool isPreview)
    {
        ArgumentNullException.ThrowIfNull(screen);
        SubjectId = subjectId;
        Kind = kind;
        Screen = screen;
        _title = title;
        _isPreview = isPreview;
    }

    /// <summary>Promote a preview tab to a persistent one (no-op if already pinned).</summary>
    public void Pin() => IsPreview = false;
}
