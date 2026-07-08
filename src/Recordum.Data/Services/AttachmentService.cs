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

        var ext = Path.GetExtension(sourceFilePath).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            ext = "bin";

        var attachmentId = Guid.NewGuid();
        var noteFolder = Path.Combine(_attachmentsRoot, noteId.ToString());
        Directory.CreateDirectory(noteFolder);

        var destPath = Path.Combine(noteFolder, $"{attachmentId:N}.{ext}");
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
        var path = Path.Combine(_attachmentsRoot, entity.NoteId.ToString(),
            $"{entity.Id:N}{Path.GetExtension(entity.Filename)}");
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
