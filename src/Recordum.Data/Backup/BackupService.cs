using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Core.Models;
using Recordum.Data.Security;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Backup;

// All constructor parameters are used: dbFactory→BuildManifestAsync/ApplyRetentionAsync; keyProvider→DumpDbAsync
public sealed class BackupService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IDbKeyProvider keyProvider,
    IBackupStoragePaths paths,
    IBackupCipher cipher,
    BackupArchiveWriter writer,
    IBackupLogService log,
    IPluginManager plugins,
    IClock clock,
    string appVersion) : IBackupService
{
    // Fallback only; the real value is read from AppSettings at manifest-build time (see
    // DbSchemaVersion). Keep this aligned with AppSettingSeed's schema_version so it never drifts
    // stale relative to the actual current schema (same class of issue as CR8).
    private const string FallbackSchemaVersion = "2.15";

    public async Task<BackupResult> CreateAsync(string destinationPath, string passphrase, BackupOptions options, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);

        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        var tempDir = Path.Combine(paths.TempDirectory, $"recordum-bk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDb = Path.Combine(tempDir, "recordum.db");
        var tempAttachments = Path.Combine(tempDir, "attachments");
        Directory.CreateDirectory(tempAttachments);
        var tempZip = Path.Combine(tempDir, "inner.zip");

        try
        {
            // 1. Plain DB dump via VACUUM INTO
            await DumpDbAsync(tempDb, ct).ConfigureAwait(false);

            // 2. Copy attachments (recursive: photos live under attachments/{noteId}/{id}.{ext} —
            // a flat top-level-only copy would silently drop every attachment, since none of them
            // sit directly in AttachmentsDirectory itself).
            if (Directory.Exists(paths.AttachmentsDirectory))
            {
                foreach (var f in Directory.EnumerateFiles(paths.AttachmentsDirectory, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(paths.AttachmentsDirectory, f);
                    var dest = Path.Combine(tempAttachments, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(f, dest, overwrite: true);
                }
            }

            // 3. Build manifest
            var manifest = await BuildManifestAsync(options.Kind, ct).ConfigureAwait(false);

            // 4. Stream-zip
            await using (var zfs = File.Create(tempZip))
                await writer.WriteAsync(zfs, manifest, tempDb, tempAttachments, ct).ConfigureAwait(false);

            // 5. Encrypt envelope to destination
            await using (var src = File.OpenRead(tempZip))
            await using (var dst = File.Create(destinationPath))
                await cipher.EncryptAsync(src, dst, passphrase, ct).ConfigureAwait(false);

            // 6. Log row
            var id = Guid.NewGuid();
            var row = new BackupLog
            {
                Id = id,
                CreatedAt = clock.UtcNow.UtcDateTime,
                Kind = options.Kind.ToString(),
                Path = destinationPath,
                SizeBytes = new FileInfo(destinationPath).Length,
                SchemaVersion = manifest.SchemaVersion,
                AppVersion = manifest.AppVersion,
            };
            await log.RecordAsync(row, ct).ConfigureAwait(false);

            // 7. Retention for Auto
            if (options.Kind == BackupKind.Auto)
                await ApplyRetentionAsync(ct).ConfigureAwait(false);

            return new BackupResult(id, destinationPath, row.SizeBytes, manifest);
        }
        catch (Exception ex) when (ex is not BackupException)
        {
            try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
            throw;
        }
        finally
        {
            ShredDirectory(tempDir);
        }
    }

    private async Task DumpDbAsync(string targetPath, CancellationToken ct)
    {
        // Plaintext copy. VACUUM INTO keeps the source's cipher and key (D9 probe §2, trap 2: a backup that restores only
        // on this machine), so the copy is then opened with the same key and decrypted in place.
        // The source is opened directly (not via EF Core) to control the key pragmas.
        using var srcKey = await keyProvider.GetKeyAsync(ct).ConfigureAwait(false);
        var dbPath = paths.DatabasePath;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using (var conn = SqlCipherConnectionFactory.Open(dbPath, srcKey))
                using (var vacuum = conn.CreateCommand())
                {
                    vacuum.CommandText = "VACUUM INTO @target";
                    vacuum.Parameters.AddWithValue("@target", targetPath);
                    await vacuum.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                using var copy = SqlCipherConnectionFactory.Open(targetPath, srcKey);
                try
                {
                    using var rekey = copy.CreateCommand();
                    rekey.CommandText = CipherPragmas.Rekey([]);
                    await rekey.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    // Out of the pool: an idle pooled connection would keep the plaintext file open, and on Windows the
                    // shredding in CreateAsync would silently fail to delete it.
                    SqliteConnection.ClearPool(copy);
                }
                return;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 /* SQLITE_BUSY */ && attempt < 2)
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
                throw new BackupException("db_busy_retry_later", ex.Message);
            }
        }
    }

    private async Task<BackupManifest> BuildManifestAsync(BackupKind kind, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var habits = await db.HabitNodes.IgnoreQueryFilters()
            .CountAsync(x => x.NodeType == Core.Enums.NodeType.Habit && x.DeletedAt == null, ct).ConfigureAwait(false);
        var completions = await db.Completions.CountAsync(ct).ConfigureAwait(false);
        var notes = await db.Notes.CountAsync(ct).ConfigureAwait(false);
        var attachments = await db.Attachments.CountAsync(ct).ConfigureAwait(false);

        var pluginSnapshots = plugins.ListActivatedManifests()
            .Select(m => new PluginManifestSnapshot(m.Id, m.Version, true, false))
            .ToArray();

        var schemaVersion = await DbSchemaVersion.ReadAsync(db, FallbackSchemaVersion, ct).ConfigureAwait(false);
        return new BackupManifest(
            SchemaVersion: schemaVersion,
            AppVersion: appVersion,
            CreatedAtUtc: clock.UtcNow.UtcDateTime,
            Kind: kind,
            SizeBytesUncompressed: 0,
            Counts: new BackupCounts(habits, completions, notes, attachments),
            Plugins: pluginSnapshots,
            Permissions: new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));
    }

    private async Task ApplyRetentionAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var retention = await db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "backup.auto.retention_count")
            .Select(x => x.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (!int.TryParse(retention, out var count) || count <= 0) return;

        var autos = await db.BackupLogs.AsNoTracking()
            .Where(x => x.Kind == "Auto")
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var stale in autos.Skip(count))
        {
            try { if (File.Exists(stale.Path)) File.Delete(stale.Path); } catch { }
            await log.DeleteAsync(stale.Id, ct).ConfigureAwait(false);
        }
    }

    // Secure-wipe shared with RestoreService (see SecureWipe).
    private static void ShredDirectory(string dir) => SecureWipe.DeleteDirectory(dir);
}
