namespace Recordum.Plugins.Contracts.Events;

public sealed record CompletionDeletedPayload(Guid CompletionId, Guid HabitNodeId, DateTime CompletedAt) : IEventPayload
{
    public EventName Name => WellKnownEvents.CompletionDeleted;
}
