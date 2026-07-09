using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class HabitNode
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public NodeType NodeType { get; set; }
    public HabitType? HabitType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public PinScope PinScope { get; set; }
    public int Priority { get; set; }
    public bool IsLocked { get; set; }
    public AiDataSharing AiDataSharing { get; set; } = AiDataSharing.None;
    public TimeOnly? ResetTime { get; set; }
    public string? CreatedByPluginId { get; set; }

    // Onda 2: per-node presentation preferences (JSON). NULL = type defaults. Not versioned.
    public string? UiPrefsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }
    public DateTime? DeletedAt { get; set; }

    public HabitNode? Parent { get; set; }
    public List<HabitNode> Children { get; set; } = [];
    public HabitConfig? Config { get; set; }
    public List<HabitConfigHistory> ConfigHistory { get; set; } = [];
    public List<MetricDefinition> Metrics { get; set; } = [];
    public List<SegmentDefinition> Segments { get; set; } = [];
    public List<Completion> Completions { get; set; } = [];
    public List<StreakPause> StreakPauses { get; set; } = [];
    public List<Note> Notes { get; set; } = [];
    public List<Goal> Goals { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
    public List<HabitNodeTag> Tags { get; set; } = [];
}
