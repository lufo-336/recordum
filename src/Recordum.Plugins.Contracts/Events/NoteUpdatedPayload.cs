namespace Recordum.Plugins.Contracts.Events;

public sealed record NoteUpdatedPayload(Guid NoteId, Guid HabitNodeId) : IEventPayload
{
    public EventName Name => WellKnownEvents.NoteUpdated;
}
