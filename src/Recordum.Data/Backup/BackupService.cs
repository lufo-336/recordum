using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Backup;

#pragma warning disable CS9113   // keyProvider reserved for future RestoreService symmetry
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
    private const string CurrentSchemaVersion = "2.7";

    public async Task<BackupResult> CreateAsync(string destinationPath, string passphrase, BackupOptions options, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);

        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        var tempDir = Path.Combine(Path.GetTempPath(), $"recordum-bk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDb = Path.Combine(tempDir, "recordum.db");
        var tempAttachments = Path.Combine(tempDir, "attachments");
        Directory.CreateDirectory(tempAttachments);
        var tempZip = Path.Combine(tempDir, "inner.zip");

        try
        {
            // 1. Plain DB dump via VACUUM INTO
            await DumpDbAsync(tempDb, ct).ConfigureAwait(false);

            // 2. Copy attachments
            if (Directory.Exists(paths.AttachmentsDirectory))
            {
                foreach (var f in Directory.EnumerateFiles(paths.AttachmentsDirectory))
                    File.Copy(f, Path.Combine(tempAttachments, Path.GetFileName(f)), overwrite: true);
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
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct).ConfigureAwait(false);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "VACUUM INTO $p";
                cmd.Parameters.AddWithValue("$p", targetPath);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 /* SQLITE_BUSY */ && attempt < 2)
            {
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

        return new BackupManifest(
            SchemaVersion: CurrentSchemaVersion,
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

    private static void ShredDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(f);
                    using var fs = File.Open(f, FileMode.Open, FileAccess.Write);
                    var zero = new byte[Math.Min(fi.Length, 65_536)];
                    var remaining = fi.Length;
                    while (remaining > 0)
                    {
                        var n = (int)Math.Min(zero.Length, remaining);
                        fs.Write(zero, 0, n);
                        remaining -= n;
                    }
                }
                catch { }
            }
            Directory.Delete(dir, recursive: true);
        }
        catch { }
    }
}
