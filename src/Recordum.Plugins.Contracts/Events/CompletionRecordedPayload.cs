namespace Recordum.Plugins.Contracts.Events;

public sealed record CompletionRecordedPayload(Guid CompletionId, Guid HabitNodeId, DateTime CompletedAt) : IEventPayload
{
    public EventName Name => WellKnownEvents.CompletionRecorded;
}
