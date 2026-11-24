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
    /// <summary>The (normalized) photo bytes, stored inside the encrypted DB. Null for legacy file-backed rows not yet imported.</summary>
    public byte[]? Content { get; set; }
    public byte[]? Thumbnail { get; set; }
    /// <summary>True when a legacy row's file is gone and there is no content to show.</summary>
    public bool IsMissing { get; set; }
    /// <summary>Nome del dispositivo dove sta l'originale (null se aggiunta prima dell'identità di unione).</summary>
    public string? OriginDeviceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public Note Note { get; set; } = null!;
}
