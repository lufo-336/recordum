namespace Recordum.Plugins.Contracts.Events;

/// <summary>An abstinence period opened, closed or was deleted (W2V-6). Resistance habits are the one
/// kind whose data changes without a completion, so without this no surface could learn that the
/// state it is displaying — including a running clock — is no longer true.
///
/// <para>One payload for the three events instead of three near-identical records: every listener so
/// far reloads the same way whatever happened, and the event name still distinguishes them for anyone
/// who needs to.</para></summary>
public sealed record AbstinenceChangedPayload(Guid HabitNodeId, EventName Name) : IEventPayload;
