using System;
using Recordum.Core.Models;

namespace Recordum.Data.Models;

/// <summary>EF Core entity: photo or file attached to a Note.</summary>
public class Attachment
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public Note Note { get; set; } = null!;
}
