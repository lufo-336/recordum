using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IReferenceDataQueryService
{
    ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default);
}
