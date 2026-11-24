using System.Globalization;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

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

    /// <summary>Vero quando l'originale a piena risoluzione è presente nel database locale.</summary>
    public bool HasContent { get; }

    /// <summary>Nome del dispositivo dove sta l'originale, o null se sconosciuto.</summary>
    public string? OriginDeviceName { get; }

    /// <summary>True when a legacy photo's file is gone and there is nothing to show.</summary>
    public bool IsMissing { get; }

    /// <summary>Niente da mostrare: la view disegna l'icona "foto non disponibile" al posto della miniatura.</summary>
    public bool ShowsPlaceholder => IsMissing || Thumbnail is null;

    /// <summary>Scritta sotto la miniatura e nel visore quando l'originale non è su questo dispositivo.</summary>
    public string? OriginalElsewhereText
    {
        get
        {
            if (HasContent || IsMissing) return null;
#pragma warning disable CA1863 // Strings.Culture può cambiare a runtime (anche nei test): nessuna format statica
            return string.IsNullOrWhiteSpace(OriginDeviceName)
                ? Strings.Notes_Photo_OriginalElsewhere
                : string.Format(CultureInfo.CurrentCulture, Strings.Notes_Photo_OriginalOn, OriginDeviceName);
#pragma warning restore CA1863
        }
    }

    public AttachmentViewModel(Attachment attachment, byte[]? thumbnail)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        Id = attachment.Id;
        NoteId = attachment.NoteId;
        Filename = attachment.Filename;
        MimeType = attachment.MimeType;
        FileSizeBytes = attachment.FileSizeBytes;
        Thumbnail = thumbnail;
        HasContent = attachment.HasContent;
        OriginDeviceName = attachment.OriginDeviceName;
        IsMissing = attachment.IsMissing;
    }
}
