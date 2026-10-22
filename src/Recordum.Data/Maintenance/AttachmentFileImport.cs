using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Data.Services;

namespace Recordum.Data.Maintenance;

/// <summary>Cosa ha fatto un giro dell'import. Un secondo giro sullo stesso stato torna tutto a zero.</summary>
public sealed record AttachmentImportReport(int Imported, int LeftoversDeleted, int OrphansDeleted, int Skipped, int MarkedMissing);

/// <summary>
/// F2d-1 decisione 7: le foto non stanno più su disco ma nel database cifrato. A ogni avvio i file del vecchio
/// layout <c>attachments/{noteId}/{idN}.{ext}</c> entrano nelle righe e spariscono. Idempotente.
/// <para>Un file si cancella solo (a) dopo il commit della sua riga, (b) se la riga ha già il contenuto
/// (avanzo di un crash fra commit e cancellazione), (c) se si chiama come un Guid ma non ha riga (il vecchio
/// <c>DeleteEntryAsync</c> lasciava i file). Tutto il resto resta dov'è. Il log scrive solo id e conteggi.</para>
/// </summary>
public sealed partial class AttachmentFileImport(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IPhotoImporter photos,
    IBackupStoragePaths paths,
    ILogger<AttachmentFileImport> logger)
{
    public async Task<AttachmentImportReport> RunAsync(CancellationToken ct)
    {
        int imported = 0, leftovers = 0, orphans = 0, skipped = 0;
        var root = paths.AttachmentsDirectory;

        if (Directory.Exists(root))
        {
            List<string> dirs;
            try { dirs = Directory.EnumerateDirectories(root).ToList(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Radice illeggibile: non si sa quali file esistano, quindi nemmeno si marca niente come mancante.
                LogRootUnreadable();
                return new AttachmentImportReport(0, 0, 0, 0, 0);
            }
            foreach (var dir in dirs)
            {
                List<string> files;
                try { files = Directory.EnumerateFiles(dir).ToList(); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    skipped++;
                    LogFolderSkipped(Path.GetFileName(dir));
                    continue;
                }
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!Guid.TryParseExact(name, "N", out var id)) { LogLeftInPlace(); skipped++; continue; }

                    try
                    {
                        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                        var row = await db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);
                        if (row is null) { File.Delete(file); orphans++; LogOrphan(id); continue; }
                        if (row.Content is not null) { File.Delete(file); leftovers++; continue; }

                        // Un BLOB enorme farebbe fallire SaveChanges a ogni avvio: oltre il tetto del normalizzatore il file resta.
                        if (new FileInfo(file).Length > SkiaPhotoImporter.MaxBytes) { skipped++; LogTooBig(id); continue; }

                        var bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
                        row.Content = bytes;
                        row.Thumbnail = photos.TryMakeThumbnail(bytes);
                        row.FileSizeBytes = bytes.Length;
                        row.IsMissing = false;
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                        imported++;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        skipped++;
                        LogSkipped(id);
                        continue;
                    }

                    // Solo dopo il commit. Se questa cancellazione fallisce, il giro dopo lo tratta da avanzo.
                    try { File.Delete(file); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LogSkipped(id); }
                }

                try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* resta, non è grave */ }
            }
        }

        var missing = await MarkMissingAsync(root, ct).ConfigureAwait(false);
        LogDone(imported, leftovers, orphans, skipped, missing);
        return new AttachmentImportReport(imported, leftovers, orphans, skipped, missing);
    }

    /// <summary>Le righe senza contenuto il cui file non esiste (nemmeno illeggibile) diventano <c>is_missing</c>.</summary>
    private async Task<int> MarkMissingAsync(string root, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.Attachments.Where(a => a.Content == null && !a.IsMissing).ToListAsync(ct).ConfigureAwait(false);
        var marked = 0;
        foreach (var row in rows)
        {
            var noteDir = Path.Combine(root, row.NoteId.ToString());
            try
            {
                if (Directory.Exists(noteDir) && Directory.EnumerateFiles(noteDir, row.Id.ToString("N") + ".*").Any()) continue;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue; // non si riesce a guardare: il file potrebbe esserci, la riga non si marca
            }
            row.IsMissing = true;
            marked++;
        }
        if (marked > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return marked;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Attachment import: {Imported} imported, {Leftovers} leftovers and {Orphans} orphans deleted, {Skipped} skipped, {Missing} marked missing")]
    private partial void LogDone(int imported, int leftovers, int orphans, int skipped, int missing);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Attachment file {Id} could not be read or deleted; left in place")]
    private partial void LogSkipped(Guid id);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Attachment file {Id} had no row (entry already deleted); file deleted")]
    private partial void LogOrphan(Guid id);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Attachments subfolder {Folder} could not be listed; left in place")]
    private partial void LogFolderSkipped(string folder);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "The attachments folder could not be listed; import skipped")]
    private partial void LogRootUnreadable();

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Attachment file {Id} is over the size cap; left in place, not imported")]
    private partial void LogTooBig(Guid id);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "A file in the attachments folder is not named after an attachment; left in place")]
    private partial void LogLeftInPlace();
}
