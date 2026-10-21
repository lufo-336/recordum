using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Recordum.Core.Models;

namespace Recordum.Core;

public interface IAttachmentService
{
    /// <summary>Stores an already-normalized photo (content and thumbnail) as a row of the encrypted
    /// database. Nothing touches the disk.</summary>
    Task<Attachment> AddAsync(Guid noteId, ImportedPhoto photo, CancellationToken ct);

    /// <summary>The photo bytes, or null when the row is unknown or has no content (legacy, missing file).</summary>
    Task<byte[]?> ReadContentAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>The thumbnail bytes, or null when the row is unknown or has none.</summary>
    Task<byte[]?> ReadThumbnailAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>Hard-deletes the attachment: removes the DB row (content included). There is no soft-delete for
    /// attachments — the attachment table has no deleted-at column, so a soft-deleted row would
    /// keep showing up in every gallery/list forever.</summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>List all attachments for a note (metadata only: BLOB columns are not loaded).</summary>
    Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct);

    /// <summary>Internal: list all attachments (for export/backup).</summary>
    Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct);

    /// <summary>Portable, forward-slash relative path matching the on-disk storage layout:
    /// "attachments/{NoteId}/{Id:N}.{ext}". Pure string formula, no disk I/O. Used to build
    /// export links (Markdown/HTML/JSON) that stay valid inside an exported archive.</summary>
    string ResolveRelativePath(Attachment attachment);
}
