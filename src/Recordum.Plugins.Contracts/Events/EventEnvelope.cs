namespace Recordum.Plugins.Contracts.Events;

public sealed record EventEnvelope(EventName Name, IEventPayload Payload, DateTimeOffset OccurredAt);
