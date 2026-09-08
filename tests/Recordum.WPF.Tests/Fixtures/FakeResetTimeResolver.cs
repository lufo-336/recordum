using Recordum.Core.Abstractions.Services;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Confine di giornata configurabile per i test dei ViewModel. Il default è
/// <see cref="DayBoundary.Utc"/> — mezzanotte UTC, cioè l'identità: è per questo che i ~90 test
/// scritti prima di R9 restano veri senza toccarli.</summary>
public sealed class FakeResetTimeResolver(DayBoundary? boundary = null) : IResetTimeResolver
{
    private readonly DayBoundary _boundary = boundary ?? DayBoundary.Utc;

    public ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default)
        => ValueTask.FromResult(_boundary.ResetTime);

    public ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default)
        => ValueTask.FromResult(_boundary);

    public ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(
        IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(habitNodeIds);
        IReadOnlyDictionary<Guid, DayBoundary> map =
            habitNodeIds.Distinct().ToDictionary(id => id, _ => _boundary);
        return ValueTask.FromResult(map);
    }

    public ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default)
        => ValueTask.FromResult(_boundary);
}
