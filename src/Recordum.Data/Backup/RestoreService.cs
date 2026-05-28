using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Backup;

#pragma warning disable CS9113   // dbFactory reserved for future schema-version checks; both params kept for API symmetry
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
    private const string CurrentSchemaVersion = "2.7";

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

            if (CompareMajor(manifest.SchemaVersion, CurrentSchemaVersion) > 0)
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

        // 3. Decrypt to temp zip and extract
        var tempZip = Path.Combine(Path.GetTempPath(), $"recordum-rs-{Guid.NewGuid():N}.zip");
        var tempPlainDb = Path.Combine(Path.GetTempPath(), $"recordum-rs-{Guid.NewGuid():N}.db");
        var dbPath = paths.DatabasePath;
        try
        {
            await using (var src = File.OpenRead(sourcePath))
            await using (var dst = File.Create(tempZip))
                await cipher.DecryptAsync(src, dst, passphrase, ct).ConfigureAwait(false);

            await reader.ExtractDbAsync(tempZip, tempPlainDb, ct).ConfigureAwait(false);

            // Clear current DB and attachments
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (Directory.Exists(paths.AttachmentsDirectory))
                foreach (var f in Directory.EnumerateFiles(paths.AttachmentsDirectory)) File.Delete(f);
            await reader.ExtractAttachmentsAsync(tempZip, paths.AttachmentsDirectory, ct).ConfigureAwait(false);

            // 4. Re-encrypt the plain DB with the current cipher key
            using var key = await keyProvider.GetKeyAsync(ct).ConfigureAwait(false);
            await ReEncryptDbAsync(tempPlainDb, dbPath, key.RawKey).ConfigureAwait(false);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (File.Exists(tempPlainDb)) File.Delete(tempPlainDb); } catch { }
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

    private static int CompareMajor(string a, string b)
    {
        _ = int.TryParse(a.Split('.').FirstOrDefault(), out var aMaj);
        _ = int.TryParse(b.Split('.').FirstOrDefault(), out var bMaj);
        return aMaj.CompareTo(bMaj);
    }
}
