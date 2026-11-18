using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>In-memory IAbstinenceService for VM tests. <see cref="Periods"/> is the source of truth;
/// Start/Lapse/DeletePeriod mutate it AND keep <see cref="States"/> coherent (open period → Abstaining,
/// else last closed → Relapsed, else NotStarted), mirroring the real service. Tests may still pre-seed
/// <see cref="States"/> directly to drive GetStateAsync without touching periods.</summary>
public sealed class FakeAbstinenceService : IAbstinenceService
{
    public Dictionary<Guid, AbstinenceState> States { get; } = new();
    public Dictionary<Guid, List<AbstinencePeriod>> Periods { get; } = new();

    /// <summary>Se valorizzato, Start/Lapse/DeletePeriod annunciano sul bus come il servizio vero.</summary>
    public IEventBus? Events { get; set; }

    public int ListPeriodsCalls { get; private set; }

    private void Publish(Guid habitNodeId, EventName name) => Events?.Publish(new AbstinenceChangedPayload(habitNodeId, name));

    public Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var state = States.TryGetValue(habitNodeId, out var s)
            ? s
            : new AbstinenceState(AbstinenceStatus.NotStarted, null, null);
        return Task.FromResult(state);
    }

    public Task StartAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        List(habitNodeId).Add(new AbstinencePeriod
        {
            Id = Guid.NewGuid(), HabitNodeId = habitNodeId, StartedAtMs = nowMs, EndedAtMs = null,
        });
        Recompute(habitNodeId);
        Publish(habitNodeId, WellKnownEvents.AbstinenceStarted);
        return Task.CompletedTask;
    }

    /// <summary>Every <c>day</c> received by <see cref="LapseAsync"/>, in call order (null = today).</summary>
    public List<DateOnly?> LapseDays { get; } = new();

    public Task LapseAsync(Guid habitNodeId, DateOnly? day = null, CancellationToken ct = default)
    {
        LapseDays.Add(day);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var open = List(habitNodeId).FirstOrDefault(p => p.EndedAtMs is null);
        if (open is not null)
        {
            open.EndedAtMs = nowMs;
            Recompute(habitNodeId);
        }
        else
        {
            // No period modelled (States-only tests): preserve the simple relapse transition.
            States[habitNodeId] = new AbstinenceState(AbstinenceStatus.Relapsed, null, nowMs);
        }
        Publish(habitNodeId, WellKnownEvents.AbstinenceLapsed);
        return Task.CompletedTask;
    }

    private List<AbstinencePeriod> List(Guid habitNodeId)
    {
        if (!Periods.TryGetValue(habitNodeId, out var list)) Periods[habitNodeId] = list = new();
        return list;
    }

    private void Recompute(Guid habitNodeId)
    {
        var list = List(habitNodeId);
        var open = list.FirstOrDefault(p => p.EndedAtMs is null);
        if (open is not null) { States[habitNodeId] = new AbstinenceState(AbstinenceStatus.Abstaining, open.StartedAtMs, null); return; }
        var lastClosed = list.Where(p => p.EndedAtMs is not null).OrderByDescending(p => p.EndedAtMs).FirstOrDefault();
        States[habitNodeId] = lastClosed is null
            ? new AbstinenceState(AbstinenceStatus.NotStarted, null, null)
            : new AbstinenceState(AbstinenceStatus.Relapsed, null, lastClosed.EndedAtMs);
    }

    public Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        ListPeriodsCalls++;
        if (!Periods.TryGetValue(habitNodeId, out var periods))
            return Task.FromResult<IReadOnlyList<AbstinencePeriod>>([]);

        var fromMs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

        IReadOnlyList<AbstinencePeriod> result = periods
            .Where(p => p.StartedAtMs <= toMs && (p.EndedAtMs is null || p.EndedAtMs.Value >= fromMs))
            .ToList();

        return Task.FromResult(result);
    }

    public Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        foreach (var (habitId, list) in Periods)
        {
            if (list.RemoveAll(p => p.Id == periodId) > 0)
            {
                Recompute(habitId);
                Publish(habitId, WellKnownEvents.AbstinencePeriodDeleted);
            }
        }
        return Task.CompletedTask;
    }
}
