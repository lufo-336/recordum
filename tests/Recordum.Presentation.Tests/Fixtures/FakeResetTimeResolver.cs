using Recordum.Core.Abstractions.Services;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>Confine di giornata configurabile per i test dei ViewModel. Il default è
/// <see cref="DayBoundary.Utc"/> — mezzanotte UTC, cioè l'identità: è per questo che i ~90 test
/// scritti prima di R9 restano veri senza toccarli.</summary>
public sealed class FakeResetTimeResolver(DayBoundary? boundary = null) : IResetTimeResolver
{
    private readonly DayBoundary _boundary = boundary ?? DayBoundary.Utc;

    /// <summary>Confini per singola abitudine, quando un test deve distinguere due soggetti — è
    /// l'unico modo di provare che un pannello ha ricevuto il confine del soggetto GIUSTO invece di
    /// quello precedente. Le abitudini non elencate ricadono sul confine di default.</summary>
    public Dictionary<Guid, DayBoundary> PerHabit { get; } = [];

    private DayBoundary For(Guid habitNodeId)
        => PerHabit.TryGetValue(habitNodeId, out var b) ? b : _boundary;

    public ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default)
        => ValueTask.FromResult(For(habitNodeId).ResetTime);

    public ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default)
        => ValueTask.FromResult(For(habitNodeId));

    public ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(
        IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(habitNodeIds);
        IReadOnlyDictionary<Guid, DayBoundary> map =
            habitNodeIds.Distinct().ToDictionary(id => id, For);
        return ValueTask.FromResult(map);
    }

    public ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default)
        => ValueTask.FromResult(_boundary);
}
