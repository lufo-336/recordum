using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitNodeService
{
    ValueTask<Guid> CreateAsync(HabitNode draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<HabitNode> mutate, CancellationToken ct = default);
    ValueTask MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default);

    /// <summary>
    /// Moves <paramref name="id"/> under <paramref name="newParentId"/> and reorders it among its
    /// siblings: placed before/after <paramref name="anchorSiblingId"/> (or appended at the end when
    /// the anchor is null). Reassigns contiguous SortOrder (0..n-1) to the sibling set. Transactional.
    /// </summary>
    ValueTask MoveAndReorderAsync(
        Guid id, Guid? newParentId, Guid? anchorSiblingId, bool placeAfter, CancellationToken ct = default);
    ValueTask SoftDeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask RestoreAsync(Guid id, CancellationToken ct = default);
    ValueTask<HabitNode?> GetAsync(Guid id, CancellationToken ct = default);
    ValueTask SetPinScopeAsync(Guid id, PinScope scope, CancellationToken ct = default);
    ValueTask SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default);
}
