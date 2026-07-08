using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Recordum.Core.Models;

namespace Recordum.Core;

public interface IAttachmentService
{
    /// <summary>Add a photo from user-selected file to the attachment folder.
    /// Copies file to %LocalAppData%\Recordum\attachments\{noteId}\{UUID}.{ext}
    /// and inserts row in attachment table.</summary>
    Task<Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct);

    /// <summary>Hard-deletes the attachment: removes the DB row and best-effort deletes the
    /// physical file on disk (a missing file is not an error). There is no soft-delete for
    /// attachments — the attachment table has no deleted-at column, so a soft-deleted row would
    /// keep showing up in every gallery/list forever.</summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>List all attachments for a note.</summary>
    Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct);

    /// <summary>Internal: list all attachments (for export/backup).</summary>
    Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct);

    /// <summary>Portable, forward-slash relative path matching the on-disk storage layout:
    /// "attachments/{NoteId}/{Id:N}.{ext}". Pure string formula, no disk I/O. Used to build
    /// export links (Markdown/HTML/JSON) that stay valid inside an exported archive.</summary>
    string ResolveRelativePath(Attachment attachment);

    /// <summary>Absolute path to the attachment's file on disk (dataRoot + <see cref="ResolveRelativePath"/>),
    /// for callers that need to read the actual bytes (export image embedding).</summary>
    string ResolveAbsolutePath(Attachment attachment);
}
