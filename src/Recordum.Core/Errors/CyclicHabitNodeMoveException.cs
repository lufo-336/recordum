namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when moving a HabitNode under a descendant would create a cycle.
/// </summary>
public sealed class CyclicHabitNodeMoveException(Guid id, Guid newParentId)
    : RecordumDomainException($"Moving {id} under {newParentId} would create a cycle.")
{
    public Guid Id { get; } = id;
    public Guid NewParentId { get; } = newParentId;
}
