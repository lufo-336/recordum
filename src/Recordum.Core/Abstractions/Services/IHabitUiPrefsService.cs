using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitUiPrefsService
{
    /// <summary>Reads a node's presentation prefs. Returns an empty record when the column is NULL.</summary>
    ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Serializes and stores a node's prefs. Throws if the node does not exist.</summary>
    ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default);

    /// <summary>Read-modify-write in one call: re-reads the current prefs immediately before applying
    /// <paramref name="mutate"/> and writing the result, so a caller holding a stale snapshot from an
    /// earlier <see cref="GetAsync"/> (e.g. taken when an editor section loaded) can never clobber a
    /// field another writer touched in the meantime. Every read-modify-write caller of
    /// <c>ui_prefs_json</c> should route through this instead of pairing <see cref="GetAsync"/> +
    /// <see cref="SetAsync"/> by hand (the 4d01f9c / 1d96967 class of data-loss bug, and the specific
    /// C2 finding this method closes: two Habit Editor sections both caching the whole record at
    /// LoadAsync and reverting each other's write).</summary>
    ValueTask UpdateAsync(Guid habitNodeId, Func<HabitUiPrefs, HabitUiPrefs> mutate, CancellationToken ct = default);
}
