using Recordum.Core.Enums;

namespace Recordum.Core.Models;

/// <summary>Una voce di diario da creare: testo, tag della riga Tag e foto già normalizzate.</summary>
public sealed record DiaryEntryDraft(
    string Text,
    IReadOnlyCollection<string> PropertyTags,
    IReadOnlyList<ImportedPhoto> Photos);

/// <summary>La modifica di una voce. <see cref="PropertyTags"/> null = riga Tag non toccata.</summary>
public sealed record DiaryEntryEdit(
    string Text,
    IReadOnlyCollection<string>? PropertyTags,
    IReadOnlyList<ImportedPhoto> AddedPhotos,
    IReadOnlyCollection<Guid> RemovedPhotoIds);

/// <summary>Un tag di una voce e da dove viene (testo o riga Tag).</summary>
public sealed record EntryTag(string Name, NoteTagSource Source);
