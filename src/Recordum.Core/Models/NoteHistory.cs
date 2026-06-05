namespace Recordum.Core.Models;

/// <summary>Immutable snapshot of a <see cref="Note"/>'s body at a point in time.</summary>
public sealed class NoteHistory
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string BodyMd { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Note Note { get; set; } = null!;
}
