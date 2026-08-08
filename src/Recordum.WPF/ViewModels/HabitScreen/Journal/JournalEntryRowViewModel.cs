using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Notes;

namespace Recordum.WPF.ViewModels.HabitScreen.Journal;

/// <summary>Una voce di diario nella lista del giorno. Nessun comando proprio: modifica ed
/// eliminazione appartengono al VM padre, come per NoteRowViewModel.</summary>
public sealed partial class JournalEntryRowViewModel(Note note) : ObservableObject
{
    public Guid Id { get; } = note.Id;
    public DateTime CreatedAt { get; } = note.CreatedAt;

    [ObservableProperty] private string _text = note.BodyMd;

    /// <summary>Testo in corso di modifica in riga; valorizzato solo mentre <see cref="IsEditing"/>.</summary>
    [ObservableProperty] private string _editDraft = string.Empty;

    [ObservableProperty] private bool _isEditing;

    public ObservableCollection<AttachmentViewModel> Photos { get; } = [];
}
