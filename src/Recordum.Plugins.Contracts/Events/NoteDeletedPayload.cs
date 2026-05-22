namespace Recordum.Plugins.Contracts.Events;

public sealed record NoteDeletedPayload(Guid NoteId, Guid HabitNodeId) : IEventPayload
{
    public EventName Name => WellKnownEvents.NoteDeleted;
}
