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

    /// <summary>Soft-delete: mark updated_at. File remains for async purge.</summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>List all non-soft-deleted attachments for a note.</summary>
    Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct);

    /// <summary>Internal: list all attachments (for export/backup).</summary>
    Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct);
}
