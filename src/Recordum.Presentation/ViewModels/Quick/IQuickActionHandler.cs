using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Quick;

public interface IQuickActionHandler
{
    HabitType HabitType { get; }

    /// <summary>
    /// Performs the per-type quick action triggered from the Home row icon.
    /// Returns true if a completion was recorded; false if the action only requested UI focus.
    /// </summary>
    ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default);
}
