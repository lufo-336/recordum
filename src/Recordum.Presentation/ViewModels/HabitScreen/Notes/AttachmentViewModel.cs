using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitScreen.Notes;

/// <summary>Plain observable data holder for one photo/file attachment on a Note. No command of
/// its own — deletion is owned by the parent VM (<see cref="NotesViewModel.DeleteAttachmentCommand"/>),
/// matching this codebase's convention where row VMs (e.g. <see cref="NoteRowViewModel"/>) don't own
/// their own destructive commands.</summary>
public sealed class AttachmentViewModel
{
    public Guid Id { get; }
    public Guid NoteId { get; }
    public string Filename { get; }
    public string MimeType { get; }
    public long FileSizeBytes { get; }

    /// <summary>The thumbnail bytes (from the database), for the image binding. Null when there is none.</summary>
    public byte[]? Thumbnail { get; }

    /// <summary>True when a legacy photo's file is gone and there is nothing to show.</summary>
    public bool IsMissing { get; }

    public AttachmentViewModel(Attachment attachment, byte[]? thumbnail)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        Id = attachment.Id;
        NoteId = attachment.NoteId;
        Filename = attachment.Filename;
        MimeType = attachment.MimeType;
        FileSizeBytes = attachment.FileSizeBytes;
        Thumbnail = thumbnail;
        IsMissing = attachment.IsMissing;
    }
}
