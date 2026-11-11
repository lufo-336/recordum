using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Quick;

public interface IQuickActionHandler
{
    HabitType HabitType { get; }

    /// <summary>
    /// Performs the per-type quick action triggered from the Home row icon.
    /// Returns true if a completion was recorded; false if the action only requested UI focus.
    /// <paramref name="day"/> is the day to record on; <c>null</c> means today (the habit's own day boundary).
    /// </summary>
    ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default);
}
