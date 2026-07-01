using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>In-memory IAbstinenceService for VM tests. Set <see cref="States"/> per habit
/// to control GetStateAsync; set <see cref="Periods"/> to control ListPeriodsAsync.</summary>
public sealed class FakeAbstinenceService : IAbstinenceService
{
    public Dictionary<Guid, AbstinenceState> States { get; } = new();
    public Dictionary<Guid, List<AbstinencePeriod>> Periods { get; } = new();

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
        States[habitNodeId] = new AbstinenceState(AbstinenceStatus.Abstaining, nowMs, null);
        return Task.CompletedTask;
    }

    public Task LapseAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        States[habitNodeId] = new AbstinenceState(AbstinenceStatus.Relapsed, null, nowMs);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
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
        foreach (var list in Periods.Values) list.RemoveAll(p => p.Id == periodId);
        return Task.CompletedTask;
    }
}
