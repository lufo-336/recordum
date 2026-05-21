namespace Recordum.Core.Errors;

public sealed class ActiveTimerNotFoundException(Guid habitNodeId)
    : RecordumDomainException($"No active timer for habit '{habitNodeId}'.")
{
    public Guid HabitNodeId { get; } = habitNodeId;
}
