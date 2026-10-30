using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.HabitEditor;

/// <summary>
/// Base class for section view-models inside the Habit Editor.
/// Provides the shared per-field autosave pipeline (Saving → Saved | Error) and the
/// progressive-disclosure flag (<see cref="ShowAdvanced"/>).
/// </summary>
public abstract partial class SectionViewModelBase : ObservableObject
{
    /// <summary>Localized section header shown on the Expander.</summary>
    public abstract string Title { get; }

    /// <summary>Whether this section applies to the loaded node (set in HabitEditorViewModel.LoadAsync).</summary>
    [ObservableProperty] private bool _isVisible;

    /// <summary>L2 progressive disclosure: collapsed by default.</summary>
    [ObservableProperty] private bool _isExpanded;

    [ObservableProperty] private EditorStatus _status = EditorStatus.Idle;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _showAdvanced;

    /// <summary>La voce in una riga, nella cultura corrente, per la riga chiusa di Configura. Mai "—": una voce
    /// senza valore dice cosa manca. Vuota = la riga non ha riassunto.</summary>
    public virtual string Summary => string.Empty;

    /// <summary>Vero se la sezione ha un livello "Avanzate" dietro il suo contenuto principale.</summary>
    public virtual bool HasAdvanced => false;

    /// <summary>Alza il cambio di <see cref="Summary"/>: ogni sezione lo chiama dopo il caricamento e dopo ogni modifica.</summary>
    protected void NotifySummary() => OnPropertyChanged(nameof(Summary));

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
