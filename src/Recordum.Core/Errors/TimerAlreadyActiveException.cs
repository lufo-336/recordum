namespace Recordum.Core.Errors;

public sealed class TimerAlreadyActiveException(Guid habitNodeId)
    : RecordumDomainException($"An active timer already exists for habit '{habitNodeId}'.")
{
    public Guid HabitNodeId { get; } = habitNodeId;
}
