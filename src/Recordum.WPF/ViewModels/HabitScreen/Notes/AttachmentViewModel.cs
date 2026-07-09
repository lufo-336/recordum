using Recordum.Core;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

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

    /// <summary>Resolved ABSOLUTE path on disk, for Image.Source binding.</summary>
    public string FilePath { get; }

    public AttachmentViewModel(Attachment attachment, IAttachmentService attachments)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(attachments);
        Id = attachment.Id;
        NoteId = attachment.NoteId;
        Filename = attachment.Filename;
        MimeType = attachment.MimeType;
        FileSizeBytes = attachment.FileSizeBytes;
        FilePath = attachments.ResolveAbsolutePath(attachment);
    }
}
