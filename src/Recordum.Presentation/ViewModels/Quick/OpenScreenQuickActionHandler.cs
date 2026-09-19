using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Quick;

/// <summary>
/// Used for habit types where one-click recording doesn't make sense (Resistance / Journal / State).
/// Returns false so the row click just opens the HabitScreen for the user to record via the QuickAdd panel.
/// </summary>
public sealed class OpenScreenQuickActionHandler(HabitType type) : IQuickActionHandler
{
    public HabitType HabitType { get; } = type;

    public ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default)
        => new(false);
}
