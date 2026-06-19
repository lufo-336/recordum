using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

/// <summary>Single write path for journal diary entries. Each entry is a Note(Diario) paired 1:1
/// with a Completion, created and deleted together transactionally — no other code may write or
/// delete the journal Note/Completion pair directly.</summary>
public interface IDiaryService
{
    /// <summary>Creates a Diario note for <paramref name="day"/> plus its paired completion, in one
    /// transaction, and returns the note id. The completion lands on <paramref name="day"/> (at the
    /// current time-of-day) so calendar/streak see the right day; it is retroactive when day != today.</summary>
    ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default);

    /// <summary>Deletes a diary entry — the note and its paired completion together — given either the
    /// note id or the completion id. No-op if neither resolves to a Diario note.</summary>
    ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default);

    /// <summary>Updates only the note body (+ version history); the paired completion is untouched.</summary>
    ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default);

    /// <summary>The Diario note paired with <paramref name="completionId"/>, or null.</summary>
    ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default);
}
