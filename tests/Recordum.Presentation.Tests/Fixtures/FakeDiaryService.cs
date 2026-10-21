using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>In-memory IDiaryService for VM tests. Models the note↔completion pair: SaveEntryAsync
/// stores a Diario note + a paired completion; DeleteEntryAsync removes both by either id.</summary>
public sealed class FakeDiaryService : IDiaryService
{
    private readonly FakeNoteService? _notes;

    /// <summary>Optional linked note store. Production DiaryService and NoteService share one
    /// database, so a saved/edited/deleted diary entry is immediately visible through
    /// INoteService too; pass the same FakeNoteService instance here to model that for VMs (like
    /// JournalQuickEntryViewModel) that write via IDiaryService and read back via INoteService.</summary>
    public FakeDiaryService(FakeNoteService? notes = null)
    {
        _notes = notes;
    }

    public Dictionary<Guid, Note> Notes { get; } = new();
    public Dictionary<Guid, Completion> Completions { get; } = new();
    public List<(Guid HabitId, DateOnly Day, string Text)> SaveCalls { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];
    public List<(Guid NoteId, string Text)> EditCalls { get; } = [];

    /// <summary>Same data as <see cref="SaveCalls"/> plus the generated NoteId, for callers that need
    /// to correlate the saved entry with what it produced (e.g. an attachment added right after).</summary>
    public List<(Guid NoteId, Guid HabitId, DateOnly Day, string Text)> SavedEntries { get; } = [];

    /// <summary>Ids actually resolved and removed by <see cref="DeleteEntryAsync"/> — unlike
    /// <see cref="DeleteCalls"/> (which logs whatever id was passed in, note or completion), this only
    /// records the note id when a matching entry was found.</summary>
    public List<Guid> DeletedIds { get; } = [];

    /// <summary>Same data as <see cref="EditCalls"/>, kept alongside it so existing callers of
    /// EditCalls are undisturbed.</summary>
    public List<(Guid NoteId, string Text)> EditedEntries { get; } = [];

    /// <summary>Optional gate a test can hold open to observe the VM mid-save (e.g. that IsSaving is
    /// actually true while the write is in flight). Consumed by the next Save/Edit call.</summary>
    public Func<Task>? NextWriteDelay { get; set; }

    private async ValueTask AwaitWriteGateAsync()
    {
        if (NextWriteDelay is null) return;
        var gate = NextWriteDelay;
        NextWriteDelay = null;
        await gate().ConfigureAwait(false);
    }

    public async ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)
    {
        await AwaitWriteGateAsync().ConfigureAwait(true);
        SaveCalls.Add((habitNodeId, day, text));
        var completionId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        SavedEntries.Add((noteId, habitNodeId, day, text));
        Completions[completionId] = new Completion
        {
            Id = completionId,
            HabitNodeId = habitNodeId,
            CompletedAt = day.ToDateTime(TimeOnly.MinValue),
            CompletedAtMs = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
        var note = new Note
        {
            Id = noteId,
            HabitNodeId = habitNodeId,
            BodyMd = text,
            Category = NoteCategory.Diario,
            TargetDate = day,
            CompletionId = completionId,
            CreatedAt = day.ToDateTime(TimeOnly.MinValue),
        };
        Notes[noteId] = note;
        if (_notes is not null) _notes.Store[noteId] = note;
        return noteId; // the method is async now: the ValueTask wrapper is implicit
    }

    public ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, DiaryEntryDraft draft, CancellationToken ct = default)
        => SaveEntryAsync(habitNodeId, day, draft.Text, ct);

    public ValueTask EditEntryAsync(Guid noteId, DiaryEntryEdit edit, CancellationToken ct = default)
        => EditEntryAsync(noteId, edit.Text, ct);

    public ValueTask<IReadOnlyDictionary<Guid, IReadOnlyList<EntryTag>>> GetEntryTagsAsync(
        IReadOnlyCollection<Guid> noteIds, CancellationToken ct = default)
        => new(new Dictionary<Guid, IReadOnlyList<EntryTag>>());

    public ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
    {
        DeleteCalls.Add(noteOrCompletionId);
        var note = Notes.Values.FirstOrDefault(n => n.Id == noteOrCompletionId || n.CompletionId == noteOrCompletionId);
        if (note is null) return ValueTask.CompletedTask;
        DeletedIds.Add(note.Id);
        Notes.Remove(note.Id);
        _notes?.Store.Remove(note.Id);
        if (note.CompletionId is { } cid) Completions.Remove(cid);
        return ValueTask.CompletedTask;
    }

    public async ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
    {
        await AwaitWriteGateAsync().ConfigureAwait(true);
        EditCalls.Add((noteId, text));
        EditedEntries.Add((noteId, text));
        if (Notes.TryGetValue(noteId, out var n)) n.BodyMd = text;
    }

    public ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
        => new(Notes.Values.FirstOrDefault(n => n.CompletionId == completionId));

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetEntryBodiesByCompletionsAsync(
        IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default)
    {
        var set = completionIds.ToHashSet();
        IReadOnlyDictionary<Guid, string> map = Notes.Values
            .Where(n => n.CompletionId is { } c && set.Contains(c))
            .ToDictionary(n => n.CompletionId!.Value, n => n.BodyMd);
        return new(map);
    }
}
