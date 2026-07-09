using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitUiPrefsService
{
    /// <summary>Reads a node's presentation prefs. Returns an empty record when the column is NULL.</summary>
    ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Serializes and stores a node's prefs. Throws if the node does not exist.</summary>
    ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default);
}
