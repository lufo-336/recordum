using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>In-memory IDiaryService for VM tests. Models the note↔completion pair: SaveEntryAsync
/// stores a Diario note + a paired completion; DeleteEntryAsync removes both by either id.</summary>
public sealed class FakeDiaryService : IDiaryService
{
    public Dictionary<Guid, Note> Notes { get; } = new();
    public Dictionary<Guid, Completion> Completions { get; } = new();
    public List<(Guid HabitId, DateOnly Day, string Text)> SaveCalls { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];
    public List<(Guid NoteId, string Text)> EditCalls { get; } = [];

    public ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)
    {
        SaveCalls.Add((habitNodeId, day, text));
        var completionId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        Completions[completionId] = new Completion
        {
            Id = completionId,
            HabitNodeId = habitNodeId,
            CompletedAt = day.ToDateTime(TimeOnly.MinValue),
            CompletedAtMs = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
        Notes[noteId] = new Note
        {
            Id = noteId,
            HabitNodeId = habitNodeId,
            BodyMd = text,
            Category = NoteCategory.Diario,
            TargetDate = day,
            CompletionId = completionId,
        };
        return new(noteId);
    }

    public ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
    {
        DeleteCalls.Add(noteOrCompletionId);
        var note = Notes.Values.FirstOrDefault(n => n.Id == noteOrCompletionId || n.CompletionId == noteOrCompletionId);
        if (note is null) return ValueTask.CompletedTask;
        Notes.Remove(note.Id);
        if (note.CompletionId is { } cid) Completions.Remove(cid);
        return ValueTask.CompletedTask;
    }

    public ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
    {
        EditCalls.Add((noteId, text));
        if (Notes.TryGetValue(noteId, out var n)) n.BodyMd = text;
        return ValueTask.CompletedTask;
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
