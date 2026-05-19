namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when attempting to restore a HabitNode whose parent is soft-deleted.
/// </summary>
public sealed class CannotRestoreOrphanException(Guid id, Guid parentId)
    : RecordumDomainException($"Cannot restore {id}: parent {parentId} is soft-deleted.")
{
    public Guid Id { get; } = id;
    public Guid ParentId { get; } = parentId;
}
