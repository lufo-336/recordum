namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when an operation is attempted on a HabitNode that is already soft-deleted.
/// </summary>
public sealed class HabitNodeAlreadySoftDeletedException(Guid id)
    : RecordumDomainException($"HabitNode {id} is soft-deleted.")
{
    public Guid Id { get; } = id;
}
