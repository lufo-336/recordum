namespace Recordum.Core.Models;

/// <summary>One abstinence attempt for a Resistance habit: opens on "Inizio" (StartedAtMs) and
/// closes on "Ricaduta" (EndedAtMs). A null EndedAtMs is the single currently-open period. The gap
/// between one period's end and the next start is "relapsed, not yet resumed" — not a clean run.</summary>
public sealed class AbstinencePeriod
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public long StartedAtMs { get; set; }
    public long? EndedAtMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
