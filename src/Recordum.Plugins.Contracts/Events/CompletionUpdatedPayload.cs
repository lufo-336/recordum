namespace Recordum.Plugins.Contracts.Events;

public sealed record CompletionUpdatedPayload(Guid CompletionId, Guid HabitNodeId, DateTime CompletedAt) : IEventPayload
{
    public EventName Name => WellKnownEvents.CompletionUpdated;
}
