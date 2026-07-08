using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recordum.Core;
using Recordum.Core.Abstractions;

namespace Recordum.Data.Services;

/// <summary>
/// CRUD for photo/file attachments on Notes. Owns both the DB row (Data.Models.Attachment,
/// table "attachment") and the physical file under the attachments root
/// (%LocalAppData%\Recordum\attachments\{noteId}\{attachmentId:N}.{ext} by default).
/// Maps the EF entity to the pure Recordum.Core.Models.Attachment DTO on every read —
/// Core stays free of EF Core references.
/// </summary>
public sealed class AttachmentService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    string dataRoot = "") : IAttachmentService
{
    // dataRoot is the Recordum data root (parent of the "attachments" folder), so the effective
    // storage path is always {dataRoot}\attachments\{noteId}\{attachmentId:N}.{ext} — by default
    // %LocalAppData%\Recordum\attachments\..., or a caller-supplied override for tests.
    private readonly string _attachmentsRoot = Path.Combine(
        string.IsNullOrEmpty(dataRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recordum")
            : dataRoot,
        "attachments");

    // Attachment is a child event of Note (like Completion/Goal) — it never carries
    // created_by_plugin_id, so pluginContext isn't consulted here. CreatedAt/UpdatedAt/
    // UpdatedAtMs are likewise populated by TimestampInterceptor from IClock on save, not by
    // this service directly. Both are still accepted as constructor dependencies for parity
    // with the rest of the service layer (IDbContextFactory, IClock, IPluginContext) and as
    // future extension points.
    private readonly IPluginContext _pluginContext = pluginContext;
    private readonly IClock _clock = clock;

    public async Task<Core.Models.Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePath);
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source file not found: {sourceFilePath}", sourceFilePath);

        var ext = ExtensionFor(sourceFilePath);
        var attachmentId = Guid.NewGuid();

        // Draft DTO carrying just enough (Id/NoteId/Filename) to derive the storage path via the
        // same formula ResolveAbsolutePath uses — the on-disk layout has exactly one source of truth.
        var draft = new Core.Models.Attachment { Id = attachmentId, NoteId = noteId, Filename = Path.GetFileName(sourceFilePath) };
        var destPath = ResolveAbsolutePath(draft);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Copy(sourceFilePath, destPath, overwrite: false);

        var entity = new Models.Attachment
        {
            Id = attachmentId,
            NoteId = noteId,
            Filename = Path.GetFileName(sourceFilePath),
            MimeType = MimeTypeFor(ext),
            FileSizeBytes = new FileInfo(destPath).Length,
            // CreatedAt/UpdatedAt/UpdatedAtMs are populated by TimestampInterceptor on save.
        };

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Attachments.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (entity is null) return;

        db.Attachments.Remove(entity);
        await db.SaveChangesAsync(ct);

        // Best-effort: an already-missing file (e.g. purged out-of-band) must not fail the delete.
        // Uses the same ResolveAbsolutePath formula AddAsync wrote to, via the shared ExtensionFor
        // helper — a prior version of this method re-derived the extension from the raw Filename
        // without lowercasing/"bin"-fallback, so an uppercase original extension (".JPG") or an
        // extensionless original wouldn't match the actual file on disk.
        var path = ResolveAbsolutePath(ToDto(entity));
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public string ResolveRelativePath(Core.Models.Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return $"attachments/{attachment.NoteId}/{attachment.Id:N}.{ExtensionFor(attachment.Filename)}";
    }

    public string ResolveAbsolutePath(Core.Models.Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return Path.Combine(_attachmentsRoot, attachment.NoteId.ToString(),
            $"{attachment.Id:N}.{ExtensionFor(attachment.Filename)}");
    }

    /// <summary>Single source of truth for the file extension derivation shared by AddAsync
    /// (from the source file path), DeleteAsync/Resolve* (from the stored original Filename):
    /// lowercased, no leading dot, falling back to "bin" when the original had no extension.</summary>
    private static string ExtensionFor(string filenameOrPath)
    {
        var ext = Path.GetExtension(filenameOrPath).TrimStart('.').ToLowerInvariant();
        return string.IsNullOrEmpty(ext) ? "bin" : ext;
    }

    public async Task<IReadOnlyList<Core.Models.Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Attachments.AsNoTracking()
            .Where(a => a.NoteId == noteId)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<Core.Models.Attachment>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Attachments.AsNoTracking().ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static Core.Models.Attachment ToDto(Models.Attachment entity) => new()
    {
        Id = entity.Id,
        NoteId = entity.NoteId,
        Filename = entity.Filename,
        MimeType = entity.MimeType,
        FileSizeBytes = entity.FileSizeBytes,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        UpdatedAtMs = entity.UpdatedAtMs,
    };

    private static string MimeTypeFor(string extension) =>
        extension switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            _ => "application/octet-stream",
        };
}
