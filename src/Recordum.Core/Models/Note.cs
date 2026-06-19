using Recordum.Core.Enums;

namespace Recordum.Core.Models;

public sealed class Note
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }

    /// <summary>Diario (journal diary entry, paired with a Completion) vs Veloce (free quick note, no completion).
    /// Column default is Veloce; legacy rows are Veloce.</summary>
    public NoteCategory Category { get; set; } = NoteCategory.Veloce;

    /// <summary>Soft FK to the paired Completion (Diario notes only; null for Veloce). The note↔completion
    /// pairing is managed transactionally by IDiaryService — there is no EF relationship or cascade here.</summary>
    public Guid? CompletionId { get; set; }

    public string BodyMd { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    /// <summary>Optional logical day this note belongs to (calendar "note of the day"). Null = free/un-anchored note.</summary>
    public DateOnly? TargetDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
    public List<Attachment> Attachments { get; set; } = [];
    public List<NoteHistory> History { get; set; } = [];
}
