namespace Recordum.Core.Models;

public sealed class AIInsight
{
    public Guid Id { get; set; }
    public Guid? HabitNodeId { get; set; }
    public string InsightType { get; set; } = string.Empty;
    public string BodyMd { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ValidUntil { get; set; }
    public int? UserRating { get; set; }
    public DateTime CreatedAt { get; set; }

    public HabitNode? HabitNode { get; set; }
}
