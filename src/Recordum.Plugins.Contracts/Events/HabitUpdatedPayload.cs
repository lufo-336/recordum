namespace Recordum.Plugins.Contracts.Events;

/// <summary>Raised when a habit node's user-visible properties (name, color, icon, …) change, so
/// views holding a cached copy (Home rows, the open workspace) can refresh.</summary>
public sealed record HabitUpdatedPayload(Guid HabitNodeId) : IEventPayload
{
    public EventName Name => WellKnownEvents.HabitUpdated;
}
