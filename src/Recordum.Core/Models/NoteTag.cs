using Recordum.Core.Enums;

namespace Recordum.Core.Models;

/// <summary>Tag attached to a note. The same note/tag pair can appear once per <see cref="Source"/>.</summary>
public sealed class NoteTag
{
    public Guid NoteId { get; set; }
    public Guid TagId { get; set; }
    public NoteTagSource Source { get; set; }
    public long UpdatedAtMs { get; set; }
}
