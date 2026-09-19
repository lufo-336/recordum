namespace Recordum.Presentation.ViewModels.HabitEditor;

/// <summary>
/// State of a single autosave operation surfaced in the editor footer.
/// Severity order (least → most): Idle &lt; Saved &lt; Saving &lt; Error.
/// </summary>
public enum EditorStatus
{
    Idle,
    Saving,
    Saved,
    Error,
}
