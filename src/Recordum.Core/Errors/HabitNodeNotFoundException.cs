namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when a HabitNode is not found by its ID.
/// </summary>
public sealed class HabitNodeNotFoundException(Guid id)
    : RecordumDomainException($"HabitNode {id} not found.")
{
    public Guid Id { get; } = id;
}
