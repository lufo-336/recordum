using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Backup;

public sealed class RestoreService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IDbKeyProvider keyProvider,
    IBackupStoragePaths paths,
    IBackupCipher cipher,
    BackupArchiveReader reader,
    IBackupService backupService,
    IPluginManager plugins,
    IClock clock) : IRestoreService
{
    // CR2: fallback only if the live DB somehow has no schema_version row; the real current
    // version is read from the DB at runtime (see GetCurrentSchemaVersionAsync).
    private const string FallbackSchemaVersion = "2.10";

    public async Task<RestorePreview> PreviewAsync(string sourcePath, string passphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        if (!File.Exists(sourcePath))
            throw new RestoreException("invalid_format", $"Backup file not found: {sourcePath}");

        var tempZip = Path.Combine(Path.GetTempPath(), $"recordum-rs-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var src = File.OpenRead(sourcePath))
            await using (var dst = File.Create(tempZip))
                await cipher.DecryptAsync(src, dst, passphrase, ct).ConfigureAwait(false);

            var manifest = await reader.ReadManifestAsync(tempZip, ct).ConfigureAwait(false);

            var currentSchema = await GetCurrentSchemaVersionAsync(ct).ConfigureAwait(false);
            if (SchemaVersionGuard.IsNewer(manifest.SchemaVersion, currentSchema))
                throw new RestoreException("incompatible_schema",
                    $"Backup was created by Recordum {manifest.AppVersion} (schema {manifest.SchemaVersion}). Update Recordum to restore it.");

            var activated = plugins.ListActivatedManifests().Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            var warnings = manifest.Plugins
                .Where(p => !activated.Contains(p.PluginId))
                .Select(p => $"Plugin '{p.PluginId}' not currently activated.")
                .ToArray();

            return new RestorePreview(manifest, warnings);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
        }
    }

    public async Task<RestoreResult> RestoreAsync(string sourcePath, string passphrase, RestoreOptions options, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(sourcePath, passphrase, ct).ConfigureAwait(false);

        // 1. Safety snapshot
        if (!options.SkipSafetySnapshot)
        {
            var snapshotPath = Path.Combine(paths.BackupsDirectory,
                $"pre-restore-{clock.UtcNow:yyyyMMdd-HHmmss}.recbak");
            try
            {
                await backupService.CreateAsync(snapshotPath, passphrase, new BackupOptions(BackupKind.PreRestore), ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new RestoreException("safety_snapshot_failed", ex.Message);
            }
        }

        // 2. Drain connection pool so we can replace the DB file
        SqliteConnection.ClearAllPools();

        // 3. Decrypt + extract into a STAGING area, re-encrypt there, then swap atomically (CR1).
        //    The live DB and attachments are not touched until both new copies are fully built.
        var tempZip = Path.Combine(Path.GetTempPath(), $"recordum-rs-{Guid.NewGuid():N}.zip");
        var tempPlainDb = Path.Combine(Path.GetTempPath(), $"recordum-rs-{Guid.NewGuid():N}.db");
        var dbPath = paths.DatabasePath;
        var attachDir = paths.AttachmentsDirectory;
        // Stage NEXT TO the targets so the final move is an atomic same-volume rename.
        var stagedDb = Path.Combine(Path.GetDirectoryName(dbPath)!, $".restore-new-{Guid.NewGuid():N}.db");
        var stagedAttach = Path.Combine(
            Path.GetDirectoryName(attachDir.TrimEnd(Path.DirectorySeparatorChar))!,
            $".restore-new-attach-{Guid.NewGuid():N}");
        try
        {
            await using (var src = File.OpenRead(sourcePath))
            await using (var dst = File.Create(tempZip))
                await cipher.DecryptAsync(src, dst, passphrase, ct).ConfigureAwait(false);

            await reader.ExtractDbAsync(tempZip, tempPlainDb, ct).ConfigureAwait(false);
            await reader.ExtractAttachmentsAsync(tempZip, stagedAttach, ct).ConfigureAwait(false);

            // Re-encrypt the plain DB into the staging file (NOT over the live DB).
            using (var key = await keyProvider.GetKeyAsync(ct).ConfigureAwait(false))
                await ReEncryptDbAsync(tempPlainDb, stagedDb, key.RawKey).ConfigureAwait(false);

            // 4. Commit: swap staged → live, keeping the old copies aside for rollback (CR1).
            CommitSwap(dbPath, stagedDb, attachDir, stagedAttach);
        }
        catch
        {
            // Staging or commit-rollback failed: the live DB/attachments are intact, and the
            // pre-restore snapshot remains as the ultimate safety net.
            try { if (File.Exists(stagedDb)) File.Delete(stagedDb); } catch { }
            try { if (Directory.Exists(stagedAttach)) Directory.Delete(stagedAttach, recursive: true); } catch { }
            throw;
        }
        finally
        {
            // CR4: these temp artifacts hold PLAINTEXT DB data — shred, don't just unlink.
            SecureWipe.DeleteFile(tempPlainDb);
            SecureWipe.DeleteFile(tempZip);
        }

        // 5. Plugin reconciliation
        var activated = plugins.ListActivatedManifests().Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var unavailable = preview.Manifest.Plugins
            .Where(p => !activated.Contains(p.PluginId))
            .Select(p => p.PluginId)
            .ToArray();
        return new RestoreResult(unavailable);
    }

    private static async Task ReEncryptDbAsync(string plainSourcePath, string destinationPath, byte[] keyBytes)
    {
        var hexKey = Convert.ToHexString(keyBytes);
        await using var conn = new SqliteConnection($"Data Source={plainSourcePath}");
        await conn.OpenAsync();
        using (var attach = conn.CreateCommand())
        {
            attach.CommandText = $"ATTACH DATABASE @p AS enc KEY \"x'{hexKey}'\"";
            attach.Parameters.AddWithValue("@p", destinationPath);
            await attach.ExecuteNonQueryAsync();
        }
        using (var export = conn.CreateCommand())
        {
            export.CommandText = "SELECT sqlcipher_export('enc')";
            await export.ExecuteScalarAsync();
        }
        using (var detach = conn.CreateCommand())
        {
            detach.CommandText = "DETACH DATABASE enc";
            await detach.ExecuteNonQueryAsync();
        }
    }

    // CR2: the real schema version this build supports = the value the live (migrated) DB carries.
    private async Task<string> GetCurrentSchemaVersionAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var version = await db.AppSettings
            .Where(a => a.Key == "schema_version")
            .Select(a => a.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(version) ? FallbackSchemaVersion : version;
    }

    // CR1: atomically replace the live DB + attachments with the staged copies, keeping the previous
    // copies aside so a mid-swap failure rolls back instead of leaving a half-restored state.
    private static void CommitSwap(string dbPath, string stagedDb, string attachDir, string stagedAttach)
    {
        string? dbOld = null;
        string? attachOld = null;
        try
        {
            if (File.Exists(dbPath))
            {
                dbOld = dbPath + ".old";
                if (File.Exists(dbOld)) File.Delete(dbOld);
                File.Move(dbPath, dbOld);
            }
            File.Move(stagedDb, dbPath);

            if (Directory.Exists(attachDir))
            {
                attachOld = attachDir.TrimEnd(Path.DirectorySeparatorChar) + ".old";
                if (Directory.Exists(attachOld)) Directory.Delete(attachOld, recursive: true);
                Directory.Move(attachDir, attachOld);
            }
            Directory.Move(stagedAttach, attachDir);
        }
        catch
        {
            // Roll back to the state we found before the swap.
            try
            {
                if (dbOld is not null && File.Exists(dbOld))
                {
                    if (File.Exists(dbPath)) File.Delete(dbPath);
                    File.Move(dbOld, dbPath);
                }
            }
            catch { /* best-effort rollback; the pre-restore snapshot is the backstop */ }
            try
            {
                if (attachOld is not null && Directory.Exists(attachOld))
                {
                    if (Directory.Exists(attachDir)) Directory.Delete(attachDir, recursive: true);
                    Directory.Move(attachOld, attachDir);
                }
            }
            catch { /* best-effort rollback */ }
            throw;
        }

        // Success: drop the superseded copies.
        try { if (dbOld is not null && File.Exists(dbOld)) File.Delete(dbOld); } catch { }
        try { if (attachOld is not null && Directory.Exists(attachOld)) Directory.Delete(attachOld, recursive: true); } catch { }
    }
}
