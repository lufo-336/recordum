using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.Presentation.ViewModels.Workspace;

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
    private bool _disposed;

    /// <summary>Re-raised from <see cref="Screen"/>: the tab's last view column was closed (close this tab).</summary>
    public event EventHandler? AllColumnsClosed;
    /// <summary>Re-raised from <see cref="Screen"/>: the tab asks to export its subject.</summary>
    public event EventHandler? ExportRequested;
    /// <summary>Re-raised from the screen's Panoramica: a child row asks to open another subject.</summary>
    public event EventHandler<Guid>? ChildSelected;

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

        Screen.AllColumnsClosed += OnScreenAllColumnsClosed;
        Screen.ExportRequested += OnScreenExportRequested;
        Screen.Panoramica.ChildSelected += OnScreenChildSelected;
    }

    private void OnScreenAllColumnsClosed(object? s, EventArgs e) => AllColumnsClosed?.Invoke(this, EventArgs.Empty);
    private void OnScreenExportRequested(object? s, EventArgs e) => ExportRequested?.Invoke(this, EventArgs.Empty);
    private void OnScreenChildSelected(object? s, Guid id) => ChildSelected?.Invoke(this, id);

    /// <summary>Promote a preview tab to a persistent one (no-op if already pinned).</summary>
    public void Pin() => IsPreview = false;

    /// <summary>Disposes the tab's DI scope, releasing its HabitScreenViewModel and scoped services. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Screen.AllColumnsClosed -= OnScreenAllColumnsClosed;
        Screen.ExportRequested -= OnScreenExportRequested;
        Screen.Panoramica.ChildSelected -= OnScreenChildSelected;
        _scope.Dispose();
    }
}
