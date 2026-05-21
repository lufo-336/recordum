using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels.HabitEditor;

/// <summary>
/// Base class for section view-models inside the Habit Editor.
/// Provides the shared per-field autosave pipeline (Saving → Saved | Error) and the
/// progressive-disclosure flag (<see cref="ShowAdvanced"/>).
/// </summary>
public abstract partial class SectionViewModelBase : ObservableObject
{
    [ObservableProperty] private EditorStatus _status = EditorStatus.Idle;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _showAdvanced;

    /// <summary>
    /// Runs the given persistence delegate and translates outcome into status transitions.
    /// Internal so tests can drive it directly; sections call it from their own OnXxxChanged partials.
    /// </summary>
    internal async Task ApplyAsync(Func<Task> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        Status = EditorStatus.Saving;
        ErrorMessage = null;
        try
        {
            await persist().ConfigureAwait(true);
            Status = EditorStatus.Saved;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = EditorStatus.Error;
        }
    }

    /// <summary>
    /// Returns true when this section is relevant for the given node/habit kind.
    /// Default: applicable to every node. Override in concrete sections.
    /// </summary>
    public virtual bool IsApplicable(NodeType node, HabitType? habit) => true;
}
