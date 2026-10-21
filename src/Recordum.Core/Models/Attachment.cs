using System;

namespace Recordum.Core.Models;

/// <summary>A photo or file attached to a Note.</summary>
public class Attachment
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    /// <summary>True when a legacy row's file is gone and there is no content to show.</summary>
    public bool IsMissing { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }
}
