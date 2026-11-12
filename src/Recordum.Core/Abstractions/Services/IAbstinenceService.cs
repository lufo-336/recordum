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

    /// <summary>Closes the open period. <paramref name="day"/> null or today: now. A past day (habit
    /// boundary): noon of that day, or its last millisecond if noon is not after the start; throws
    /// <c>AbstinenceLapseBeforeStartException</c> if the period began after that day, and
    /// <see cref="ArgumentOutOfRangeException"/> for a future day. No-op if none is open.</summary>
    Task LapseAsync(Guid habitNodeId, DateOnly? day = null, CancellationToken ct = default);

    /// <summary>Periods that intersect [from, to] (by day), newest-relevant order not guaranteed.</summary>
#pragma warning disable CA1716 // Parameter name 'to' conflicts with reserved keyword
    Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default);
#pragma warning restore CA1716

    /// <summary>Hard-deletes one abstinence period by id. No-op if it does not exist. Deleting the
    /// open period reverts the derived state to the previous closed period (Relapsed) or NotStarted.</summary>
    Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default);
}
