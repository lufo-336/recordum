namespace Recordum.Core.Models;

public sealed class Attachment
{
    public Guid Id { get; set; }
    public Guid? CompletionId { get; set; }
    public Guid? NoteId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; }

    public Completion? Completion { get; set; }
    public Note? Note { get; set; }
}
