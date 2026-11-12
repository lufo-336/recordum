namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when a relapse is logged on a day that precedes the start of the open abstinence period.
/// </summary>
public sealed class AbstinenceLapseBeforeStartException(Guid habitNodeId, DateOnly day)
    : RecordumDomainException($"Abstinence of HabitNode {habitNodeId} had not started on {day:yyyy-MM-dd}.")
{
    public Guid HabitNodeId { get; } = habitNodeId;

    public DateOnly Day { get; } = day;
}
