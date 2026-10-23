using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Text;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;

namespace Recordum.Presentation.ViewModels.HabitScreen.Journal;

/// <summary>Una voce di diario nella lista del giorno. Nessun comando proprio: modifica ed
/// eliminazione appartengono al VM padre, come per NoteRowViewModel. Foto aggiunte, foto tolte e
/// riga Tag cambiano solo in memoria finché il padre non salva (annulla = <see cref="CancelEdit"/>).</summary>
public sealed partial class JournalEntryRowViewModel : ObservableObject
{
    private List<AttachmentViewModel> _photosBeforeEdit = [];

    public JournalEntryRowViewModel(Note note, IReadOnlyList<EntryTag> tags, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(clock);
        Id = note.Id;
        CreatedAt = note.CreatedAt;
        _text = note.BodyMd;
        Tags = tags.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        // Un nome presente nei due modi si mostra come tag della riga Tag (quello scelto a parte).
        TagChips = tags.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TagChip(g.First().Name, g.Any(t => t.Source == NoteTagSource.Property)))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        PropertyTagNames = tags.Where(t => t.Source == NoteTagSource.Property).Select(t => t.Name).ToList();
        // CreatedAt è UTC; la riga mostra l'ora che l'utente ha vissuto (decisione 8). Invariant: i due punti restano due punti.
        var utc = DateTime.SpecifyKind(note.CreatedAt, DateTimeKind.Utc);
        TimeDisplay = TimeZoneInfo.ConvertTimeFromUtc(utc, clock.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);
        Photos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(UnreferencedPhotos));
    }

    public Guid Id { get; }
    public DateTime CreatedAt { get; }
    public string TimeDisplay { get; }

    /// <summary>Tutti i tag della voce (nel testo e nella riga Tag), per nome.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>I tag per la view: <see cref="TagChip.IsProperty"/> distingue la riga Tag dal <c>#</c> nel testo (due stili).</summary>
    public IReadOnlyList<TagChip> TagChips { get; }

    internal IReadOnlyList<string> PropertyTagNames { get; }

    [ObservableProperty] private string _text;

    /// <summary>Testo in corso di modifica in riga; valorizzato solo mentre <see cref="IsEditing"/>.</summary>
    [ObservableProperty] private string _editDraft = string.Empty;

    [ObservableProperty] private bool _isEditing;

    public ObservableCollection<AttachmentViewModel> Photos { get; } = [];

    /// <summary>Le foto della voce il cui riferimento non e' nel testo (quello in modifica, se si sta modificando):
    /// in modalita' diario la striscia di miniature mostra solo queste, le altre stanno nel testo.</summary>
    public IReadOnlyList<AttachmentViewModel> UnreferencedPhotos
    {
        get
        {
            var referenced = ReferencedIds(IsEditing ? EditDraft : Text);
            return [.. Photos.Where(p => !referenced.Contains(p.Id))];
        }
    }

    internal static HashSet<Guid> ReferencedIds(string? text) => [.. PhotoRefs.Find(text ?? string.Empty).Select(r => r.Id)];

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(UnreferencedPhotos));
    partial void OnEditDraftChanged(string value) => OnPropertyChanged(nameof(UnreferencedPhotos));
    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(UnreferencedPhotos));

    /// <summary>Qualcosa e' cambiato rispetto a quando e' iniziata la modifica (serve a chiedere conferma prima di scartarla).</summary>
    internal bool HasChanges =>
        IsEditing
        && (EditDraft != Text
            || AddedPhotos.Count > 0
            || RemovedPhotoIds.Count > 0
            || !(EditTags?.Tags ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase)
                    .SetEquals(PropertyTagNames));

    /// <summary>Riga Tag in modifica; null fuori dalla modifica (nessun template da costruire).</summary>
    [ObservableProperty] private TagEditorViewModel? _editTags;

    /// <summary>Foto scelte in modifica, ancora da salvare.</summary>
    public ObservableCollection<ImportedPhoto> AddedPhotos { get; } = [];

    /// <summary>Foto già salvate che l'utente ha tolto in modifica, ancora da cancellare.</summary>
    public List<Guid> RemovedPhotoIds { get; } = [];

    internal void BeginEdit(IReadOnlyList<string> knownTags)
    {
        EditDraft = Text;
        _photosBeforeEdit = [.. Photos];
        AddedPhotos.Clear();
        RemovedPhotoIds.Clear();
        var editor = new TagEditorViewModel { KnownNames = knownTags };
        foreach (var t in PropertyTagNames) editor.Tags.Add(t);
        EditTags = editor;
        IsEditing = true;
    }

    internal void RemovePhoto(AttachmentViewModel photo)
    {
        if (Photos.Remove(photo)) RemovedPhotoIds.Add(photo.Id);
    }

    internal void CancelEdit()
    {
        IsEditing = false;
        Photos.Clear();
        foreach (var p in _photosBeforeEdit) Photos.Add(p);
        AddedPhotos.Clear();
        RemovedPhotoIds.Clear();
        EditTags = null;
    }
}

public sealed record TagChip(string Name, bool IsProperty);
