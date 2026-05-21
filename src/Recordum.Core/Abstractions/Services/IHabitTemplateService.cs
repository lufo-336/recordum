using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitTemplateService
{
    ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Clones a HabitTemplate's PayloadJson into a new HabitNode (and HabitConfig) under the given parent.
    /// Returns the new HabitNode id.
    /// </summary>
    ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default);
}
