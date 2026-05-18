using Recordum.Core.Enums;

namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when a completion record is invalid for the target HabitNode's type.
/// </summary>
public sealed class InvalidCompletionForHabitTypeException(Guid habitNodeId, HabitType habitType, string reason)
    : RecordumDomainException($"Invalid completion for {habitType} habit {habitNodeId}: {reason}")
{
    public Guid HabitNodeId { get; } = habitNodeId;
    public HabitType HabitType { get; } = habitType;
    public string Reason { get; } = reason;
}
