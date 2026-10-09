namespace Recordum.Plugins.Contracts.Events;

/// <summary>A habit's timer started, paused, resumed or stopped. One payload for the four events (the
/// name tells them apart): every listener reloads the same way, as with <see cref="AbstinenceChangedPayload"/>.
/// Published after the change is saved, so a reload reads the new state.</summary>
public sealed record TimerChangedPayload(Guid HabitNodeId, EventName Name) : IEventPayload;
