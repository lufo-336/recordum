using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IMetricDefinitionService
{
    ValueTask<Guid> CreateAsync(MetricDefinition draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<MetricDefinition> mutate, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<MetricDefinition>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);
}
