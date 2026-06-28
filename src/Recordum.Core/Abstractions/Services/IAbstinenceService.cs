using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public enum AbstinenceStatus { NotStarted, Abstaining, Relapsed }

/// <summary>Snapshot of a Resistance habit's abstinence state. CurrentStartedAtMs is set only while
/// Abstaining; LastEndedAtMs is the most recent relapse instant (set while Relapsed).</summary>
public sealed record AbstinenceState(AbstinenceStatus Status, long? CurrentStartedAtMs, long? LastEndedAtMs);

public interface IAbstinenceService
{
    Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Opens a new abstinence period (now). No-op if one is already open.</summary>
    Task StartAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Closes the open period (now = the relapse day). No-op if none is open.</summary>
    Task LapseAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Periods that intersect [from, to] (by day), newest-relevant order not guaranteed.</summary>
#pragma warning disable CA1716 // Parameter name 'to' conflicts with reserved keyword
    Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default);
#pragma warning restore CA1716
}
