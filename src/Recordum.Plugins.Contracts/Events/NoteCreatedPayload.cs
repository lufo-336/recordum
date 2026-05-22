namespace Recordum.Plugins.Contracts.Events;

public sealed record NoteCreatedPayload(Guid NoteId, Guid HabitNodeId) : IEventPayload
{
    public EventName Name => WellKnownEvents.NoteCreated;
}
