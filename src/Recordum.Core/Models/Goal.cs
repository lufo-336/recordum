using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class Goal
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? MetricDefinitionId { get; set; }
    public decimal TargetValue { get; set; }
    public GoalPeriodType PeriodType { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public int? RollingDays { get; set; }
    public bool IsClosed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
    public MetricDefinition? MetricDefinition { get; set; }
}
