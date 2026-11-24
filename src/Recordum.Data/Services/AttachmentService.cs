using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recordum.Core;
using Recordum.Core.Abstractions;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

/// <summary>
/// CRUD for photo attachments on Notes. The photo bytes and the thumbnail live in the encrypted
/// database (columns <c>content</c>/<c>thumbnail</c>); nothing is written to disk. Maps the EF entity
/// to the pure Recordum.Core.Models.Attachment DTO on every read — Core stays free of EF Core.
/// Lists never load the BLOB columns.
/// </summary>
public sealed class AttachmentService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    IPluginContext pluginContext,
    IDeviceIdentity? deviceIdentity = null) : IAttachmentService
{
    // Attachment is a child event of Note — it never carries created_by_plugin_id; timestamps come
    // from TimestampInterceptor. Both are accepted for parity with the rest of the service layer.
    private readonly IPluginContext _pluginContext = pluginContext;
    private readonly IClock _clock = clock;

    public async Task<Core.Models.Attachment> AddAsync(Guid noteId, ImportedPhoto photo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photo);
        var entity = ToEntity(noteId, photo, deviceIdentity?.DeviceName);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Attachments.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>Builds the row for a photo; shared with <see cref="DiaryService"/> so a diary save
    /// writes note and photos in one SaveChanges.</summary>
    internal static Models.Attachment ToEntity(Guid noteId, ImportedPhoto photo, string? originDeviceName = null) => new()
    {
        Id = photo.Id,
        NoteId = noteId,
        Filename = photo.Filename,
        MimeType = photo.MimeType,
        FileSizeBytes = photo.Content.Length,
        Content = photo.Content,
        Thumbnail = photo.Thumbnail,
        OriginDeviceName = originDeviceName,
        // CreatedAt/UpdatedAt/UpdatedAtMs are populated by TimestampInterceptor on save.
    };

    public async Task<byte[]?> ReadContentAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Attachments.AsNoTracking().Where(a => a.Id == attachmentId)
            .Select(a => a.Content).FirstOrDefaultAsync(ct);
    }

    public async Task<byte[]?> ReadThumbnailAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Attachments.AsNoTracking().Where(a => a.Id == attachmentId)
            .Select(a => a.Thumbnail).FirstOrDefaultAsync(ct);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Attachments.Where(a => a.Id == attachmentId).ExecuteDeleteAsync(ct);
    }

    public string ResolveRelativePath(Core.Models.Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return $"attachments/{attachment.NoteId}/{attachment.Id:N}.{ExtensionFor(attachment.Filename)}";
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
        return await Project(db.Attachments.AsNoTracking().Where(a => a.NoteId == noteId)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Core.Models.Attachment>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Project(db.Attachments.AsNoTracking()).ToListAsync(ct);
    }

    // Projection, not ToDto: the generated SELECT must not name the content/thumbnail columns.
    private static IQueryable<Core.Models.Attachment> Project(IQueryable<Models.Attachment> q) =>
        q.Select(a => new Core.Models.Attachment
        {
            Id = a.Id,
            NoteId = a.NoteId,
            Filename = a.Filename,
            MimeType = a.MimeType,
            FileSizeBytes = a.FileSizeBytes,
            HasContent = a.Content != null,
            OriginDeviceName = a.OriginDeviceName,
            IsMissing = a.IsMissing,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            UpdatedAtMs = a.UpdatedAtMs,
        });

    private static Core.Models.Attachment ToDto(Models.Attachment entity) => new()
    {
        Id = entity.Id,
        NoteId = entity.NoteId,
        Filename = entity.Filename,
        MimeType = entity.MimeType,
        FileSizeBytes = entity.FileSizeBytes,
        HasContent = entity.Content is not null,
        OriginDeviceName = entity.OriginDeviceName,
        IsMissing = entity.IsMissing,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        UpdatedAtMs = entity.UpdatedAtMs,
    };
}
