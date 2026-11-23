using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Core.Models;
using Recordum.Data.Backup;

namespace Recordum.Data.Sync;

/// <summary>
/// L'unione attraverso la cartella di scambio (spec 2026-11-22 "Unisci" §8.2, §8.4-bis, §9). Un giro alla volta in
/// tutto il processo (<see cref="SyncRunState"/>). I file in chiaro (il file di un pari decifrato, il proprio ZIP prima
/// di cifrarlo) stanno solo in <see cref="IBackupStoragePaths.TempDirectory"/> e si distruggono con
/// <see cref="SecureWipe"/>. La frase dell'unione resta nel database: non va nel file, nei log, nei messaggi.
/// </summary>
public sealed partial class SyncService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IAppSettingService settings,
    IBackupService backupService,
    IBackupStoragePaths paths,
    SyncFileCipher cipher,
    SyncRunState runState,
    IClock clock,
    DeviceIdentity deviceIdentity,
    string appVersion,
    ILogger<SyncService>? logger = null,
    Func<string, string, ISyncFileStore>? storeFactory = null) : ISyncService
{
    public const string FolderSettingKey = "sync.folder";
    public const string PassphraseSettingKey = "sync.passphrase";
    public const string IncludePhotosSettingKey = "sync.include_photos";
    public const string LastRunMsSettingKey = "sync.last_run_ms";
    public const string JoinedSettingKey = "sync.joined";
    public const string LastWrittenSignatureSettingKey = "sync.last_written_signature";

    private const long SignatureModulus = 1_000_000_007;

    private static readonly SyncRunResult NotRun = new(Ran: false, OwnFileWritten: false, Peers: []);

    private readonly ILogger _logger = logger ?? NullLogger<SyncService>.Instance;

    private readonly Func<string, string, ISyncFileStore> _storeFactory =
        storeFactory ?? (static (folder, ownFileName) => new FolderSyncFileStore(folder, ownFileName));

    public async Task<SyncRunResult> RunAsync(CancellationToken ct)
    {
        await runState.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RunCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            runState.Gate.Release();
        }
    }

    public async Task<SyncStatus> GetStatusAsync(CancellationToken ct)
    {
        var lastRunRaw = await settings.GetAsync(LastRunMsSettingKey, ct).ConfigureAwait(false);
        DateTimeOffset? lastRun = long.TryParse(lastRunRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : null;

        // Sola lettura: l'identità si crea solo sotto il cancello (un giro, un collegamento), mai da qui.
        long ownFileBytes = 0;
        var folder = await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(folder) && await ReadIdentityAsync(ct).ConfigureAwait(false) is { } id)
        {
            var own = new FileInfo(Path.Combine(folder, BuildOwnFileName(id.Name, id.Id)));
            if (own.Exists) ownFileBytes = own.Length;
        }

        return new SyncStatus(lastRun, runState.Peers, await CountTextConflictsAsync(ct).ConfigureAwait(false), ownFileBytes);
    }

    public async Task SetPassphraseAsync(string passphrase, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        await settings.SetAsync(PassphraseSettingKey, passphrase, ct).ConfigureAwait(false);
    }

    public async Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false)))
            return SyncJoinState.NotConfigured;
        return await IsJoinedOrAutoJoinAsync(ct).ConfigureAwait(false) ? SyncJoinState.Joined : SyncJoinState.NeedsChoice;
    }

    public async Task JoinAsync(SyncJoinChoice choice, CancellationToken ct)
    {
        await runState.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var folder = await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false);
            var passphrase = await settings.GetAsync(PassphraseSettingKey, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(passphrase))
                throw new InvalidOperationException("Choose the folder and the sync passphrase before connecting.");

            // "Parti dai dati degli altri" senza un file leggibile (frase sbagliata, cartella vuota) svuoterebbe questo
            // dispositivo senza dargli niente: si rifiuta prima di toccare qualcosa.
            if (choice == SyncJoinChoice.StartFromOthers && !await AnyPeerOpensAsync(folder, passphrase, ct).ConfigureAwait(false))
                throw new InvalidOperationException("No file from another device opens in the folder.");

            // Sempre prima un backup (spec §9): se fallisce, il collegamento non va avanti. È protetto dalla frase
            // dell'unione, l'unica che l'utente ha appena scritto e conosce.
            Directory.CreateDirectory(paths.BackupsDirectory);
            var backupPath = Path.Combine(
                paths.BackupsDirectory,
                $"pre-sync-join-{clock.UtcNow.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.recbak");
            await backupService.CreateAsync(backupPath, passphrase, new BackupOptions(BackupKind.PreRestore), ct).ConfigureAwait(false);

            if (choice == SyncJoinChoice.StartFromOthers)
                await EmptyTravellingTablesAsync(ct).ConfigureAwait(false);

            await settings.SetAsync(JoinedSettingKey, "true", ct).ConfigureAwait(false);
            await RunCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            runState.Gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        await runState.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await db.AppSettings
                .Where(s => s.Key == FolderSettingKey || s.Key == JoinedSettingKey)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            runState.SetPeers([]);
        }
        finally
        {
            runState.Gate.Release();
        }
    }

    public Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        return cipher.DecryptAsync(rcsync, zipOut, passphrase, ct);
    }

    public Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        return cipher.EncryptAsync(zipIn, rcsyncOut, passphrase, ct);
    }

    /// <summary><c>recordum-&lt;nome con solo [A-Za-z0-9-]&gt;-&lt;prime 8 cifre del device_id&gt;.rcsync</c>.</summary>
    public static string BuildOwnFileName(string deviceName, string deviceId)
    {
        var name = string.Concat(deviceName.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-'));
        return $"recordum-{name}-{deviceId[..Math.Min(8, deviceId.Length)]}.rcsync";
    }

    private async Task<SyncRunResult> RunCoreAsync(CancellationToken ct)
    {
        var folder = await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false);
        var passphrase = await settings.GetAsync(PassphraseSettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(passphrase)
            || !await IsJoinedOrAutoJoinAsync(ct).ConfigureAwait(false))
            return NotRun;

        var (deviceId, deviceName) = await EnsureIdentityAsync(ct).ConfigureAwait(false);
        var ownFileName = BuildOwnFileName(deviceName, deviceId);
        var store = _storeFactory(folder, ownFileName);
        var includePhotos = string.Equals(
            await settings.GetAsync(IncludePhotosSettingKey, ct).ConfigureAwait(false), "true", StringComparison.OrdinalIgnoreCase);

        var peers = new List<SyncPeerStatus>();
        var merged = false;
        foreach (var file in await store.ListPeerFilesAsync(ct).ConfigureAwait(false))
        {
            var tempZip = NewTempPath("in");
            try
            {
                await using var plain = await DecryptPeerAsync(store, file, passphrase, tempZip, ct).ConfigureAwait(false);
                using var snapshot = SyncArchiveReader.Open(plain);
                var name = string.IsNullOrWhiteSpace(snapshot.Manifest.DeviceName) ? file.DisplayName : snapshot.Manifest.DeviceName;
                var writtenAt = DateTimeOffset.FromUnixTimeMilliseconds(snapshot.Manifest.WrittenAtMs);
                if (string.Equals(snapshot.Manifest.DeviceId, deviceId, StringComparison.Ordinal))
                {
                    peers.Add(new SyncPeerStatus(name, writtenAt, SyncPeerProblem.SameIdentity));
                    continue;
                }

                try
                {
                    await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                    var report = await SyncMerger.MergeAsync(db, snapshot, deviceId, includePhotos, clock, ct).ConfigureAwait(false);
                    merged |= report.Inserted + report.Updated + report.Deleted + report.Repaired > 0;
                    peers.Add(new SyncPeerStatus(name, writtenAt, Problem: null));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // La transazione dell'unione annulla questo file; un file che non si unisce non ferma gli altri.
                    LogMergeFailed(_logger, file.DisplayName, ex);
                    peers.Add(new SyncPeerStatus(name, writtenAt, SyncPeerProblem.BrokenFile));
                }
            }
            catch (Exception ex) when (Classify(ex) is { } problem)
            {
                peers.Add(new SyncPeerStatus(file.DisplayName, file.LastModified, problem));
            }
            finally
            {
                SecureWipe.DeleteFile(tempZip);
            }
        }

        var ownFileWritten = await WriteOwnFileIfChangedAsync(
            store, Path.Combine(folder, ownFileName), passphrase, deviceId, deviceName, includePhotos, merged, ct).ConfigureAwait(false);

        await settings.SetAsync(LastRunMsSettingKey, clock.UtcNowMs.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        runState.SetPeers(peers);
        return new SyncRunResult(Ran: true, ownFileWritten, peers);
    }

    private static SyncPeerProblem? Classify(Exception ex) => ex switch
    {
        SyncFileException { Error: SyncFileError.CannotOpen } => SyncPeerProblem.CannotOpen,
        SyncFileException => SyncPeerProblem.BrokenFile,
        SyncFormatException { IsNewerMajorVersion: true } => SyncPeerProblem.NewerFormat,
        SyncFormatException => SyncPeerProblem.BrokenFile,
        // Un file che il client del cloud sta ancora scrivendo o tiene bloccato: si riprova al giro dopo.
        IOException or UnauthorizedAccessException => SyncPeerProblem.CannotOpen,
        _ => null,
    };

    /// <summary>Decifra il file del pari in <paramref name="tempZip"/> e lo riapre in lettura; il chiamante lo distrugge.</summary>
    private async Task<FileStream> DecryptPeerAsync(
        ISyncFileStore store, SyncPeerFile file, string passphrase, string tempZip, CancellationToken ct)
    {
        await using (var encrypted = await store.OpenReadAsync(file, ct).ConfigureAwait(false))
        await using (var plain = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await cipher.DecryptAsync(encrypted, plain, passphrase, ct).ConfigureAwait(false);
        return new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private async Task<bool> AnyPeerOpensAsync(string folder, string passphrase, CancellationToken ct)
    {
        var (deviceId, deviceName) = await EnsureIdentityAsync(ct).ConfigureAwait(false);
        var store = _storeFactory(folder, BuildOwnFileName(deviceName, deviceId));
        foreach (var file in await store.ListPeerFilesAsync(ct).ConfigureAwait(false))
        {
            var tempZip = NewTempPath("check");
            try
            {
                await using var plain = await DecryptPeerAsync(store, file, passphrase, tempZip, ct).ConfigureAwait(false);
                using var snapshot = SyncArchiveReader.Open(plain);
                if (!string.Equals(snapshot.Manifest.DeviceId, deviceId, StringComparison.Ordinal))
                    return true;
            }
            catch (Exception ex) when (Classify(ex) is not null)
            {
                // si prova il file successivo
            }
            finally
            {
                SecureWipe.DeleteFile(tempZip);
            }
        }

        return false;
    }

    private async Task<bool> WriteOwnFileIfChangedAsync(
        ISyncFileStore store, string ownPath, string passphrase, string deviceId, string deviceName, bool includePhotos,
        bool merged, CancellationToken ct)
    {
        var signature = await ComputeSignatureAsync(includePhotos, ct).ConfigureAwait(false);
        var lastSignature = await settings.GetAsync(LastWrittenSignatureSettingKey, ct).ConfigureAwait(false);
        if (!merged && File.Exists(ownPath) && string.Equals(signature, lastSignature, StringComparison.Ordinal))
            return false;

        var tempZip = NewTempPath("out");
        try
        {
            await using (var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
            await using (var zip = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var options = new SyncWriteOptions(deviceId, deviceName, appVersion, includePhotos, clock.UtcNowMs);
                await SyncArchiveWriter.WriteAsync(db, zip, options, ct).ConfigureAwait(false);
            }

            await store.WriteOwnAsync(async target =>
            {
                await using var zip = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read);
                await cipher.EncryptAsync(zip, target, passphrase, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            SecureWipe.DeleteFile(tempZip);
        }

        await settings.SetAsync(LastWrittenSignatureSettingKey, signature, ct).ConfigureAwait(false);
        return true;
    }

    private string NewTempPath(string kind)
    {
        Directory.CreateDirectory(paths.TempDirectory);
        return Path.Combine(paths.TempDirectory, $"recordum-sync-{kind}-{Guid.NewGuid():N}.zip");
    }

    /// <summary>Vero se il dispositivo è collegato; un database senza dati dell'utente si collega da solo.</summary>
    private async Task<bool> IsJoinedOrAutoJoinAsync(CancellationToken ct)
    {
        if (string.Equals(await settings.GetAsync(JoinedSettingKey, ct).ConfigureAwait(false), "true", StringComparison.Ordinal))
            return true;

        await using (var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            if (await db.HabitNodes.IgnoreQueryFilters().AnyAsync(ct).ConfigureAwait(false)
                || await db.Notes.AnyAsync(ct).ConfigureAwait(false))
                return false;
        }

        await settings.SetAsync(JoinedSettingKey, "true", ct).ConfigureAwait(false);
        return true;
    }

    private async Task<(string Id, string Name)?> ReadIdentityAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var meta = await db.SyncMeta.AsNoTracking().FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (meta is null || string.IsNullOrWhiteSpace(meta.DeviceId))
            return null;
        return (meta.DeviceId, string.IsNullOrWhiteSpace(meta.DeviceName) ? Environment.MachineName : meta.DeviceName);
    }

    /// <summary>L'identità alla prima richiesta (e dopo un ripristino, che la cancella): GUID "D" maiuscolo e il nome
    /// della macchina. Si chiama solo sotto il cancello, così due giri non creano due righe.</summary>
    private async Task<(string Id, string Name)> EnsureIdentityAsync(CancellationToken ct)
    {
        if (await ReadIdentityAsync(ct).ConfigureAwait(false) is { } existing)
            return existing;

        var meta = new SyncMeta
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid().ToString("D").ToUpperInvariant(),
            DeviceName = Environment.MachineName,
            CreatedAt = clock.UtcNow.UtcDateTime,
        };
        await using (var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            await db.SyncMeta.ExecuteDeleteAsync(ct).ConfigureAwait(false);   // un'eventuale riga senza device_id
            db.SyncMeta.Add(meta);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        deviceIdentity.Invalidate();
        return (meta.DeviceId, meta.DeviceName);
    }

    /// <summary>"N voci da sistemare": i blocchi della regola B nelle note e nel diario.</summary>
    private async Task<int> CountTextConflictsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        // Ogni blocco inizia con "— " (trattino lungo): il filtro in SQL evita di caricare tutto il diario.
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.BodyMd != null && n.BodyMd.Contains("— "))
            .Select(n => n.BodyMd!)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var diary = await db.Completions.AsNoTracking()
            .Where(c => c.Notes != null && c.Notes.Contains("— "))
            .Select(c => c.Notes!)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return notes.Concat(diary).Sum(TextConflictBlock.Count);
    }

    /// <summary>Cambia quando cambia qualcosa che il proprio file porta: le righe che viaggiano (quante, l'ora più
    /// recente e una somma delle ore, così si vede anche una modifica con un'ora più vecchia del massimo, quando un pari
    /// ha l'orologio avanti), le lapidi e la scelta delle foto.</summary>
    private async Task<string> ComputeSignatureAsync(bool includePhotos, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        var conn = (SqliteConnection)db.Database.GetDbConnection();

        long rows = 0, max = 0, sum = 0;
        foreach (var entity in SyncCatalog.Entities)
        {
            var keys = entity.PublicName == "app_setting" ? SyncCatalog.TravellingSettingKeys : null;
            var (r, m, s) = await AggregateAsync(conn, entity.Table, "updated_at_ms", keys, ct).ConfigureAwait(false);
            rows += r;
            max = Math.Max(max, m);
            sum = (sum + s) % SignatureModulus;
        }

        var (tombs, tombMax, tombSum) = await AggregateAsync(conn, "sync_tombstone", "deleted_at_ms", null, ct).ConfigureAwait(false);
        return string.Create(CultureInfo.InvariantCulture, $"{rows}:{max}:{sum}:{tombs}:{tombMax}:{tombSum}:{includePhotos}");
    }

    private static async Task<(long Rows, long Max, long Sum)> AggregateAsync(
        SqliteConnection conn, string table, string msColumn, IReadOnlySet<string>? keys, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        var where = keys is null ? "" : $" WHERE \"key\" IN ({KeyParameters(cmd, keys)})";
#pragma warning disable CA2100 // tabelle e colonne dal catalogo congelato, valori come parametri
        cmd.CommandText = $"SELECT COUNT(*), COALESCE(MAX(\"{msColumn}\"), 0), COALESCE(SUM(\"{msColumn}\" % {SignatureModulus}), 0) " +
                          $"FROM \"{table}\"{where}";
#pragma warning restore CA2100
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await reader.ReadAsync(ct).ConfigureAwait(false);
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2) % SignatureModulus);
    }

    private static string KeyParameters(SqliteCommand cmd, IReadOnlySet<string> keys) =>
        string.Join(", ", keys.Select((key, i) =>
        {
            cmd.Parameters.AddWithValue($"@k{i}", key);
            return $"@k{i}";
        }));

    /// <summary>"Parti dai dati degli altri": svuota tutto ciò che viaggia (delle impostazioni solo le chiavi che
    /// viaggiano) in una transazione sulla connessione aperta da EF con <c>Applying</c> acceso, così i trigger non
    /// lasciano lapidi: le righe di qui spariscono solo qui, mai sugli altri. Via anche lapidi e base di prima.</summary>
    private async Task EmptyTravellingTablesAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        var state = SyncConnectionState.For(conn);
        await using var efTx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var tx = (SqliteTransaction)efTx.GetDbTransaction();
        state.Applying = true;
        try
        {
            // RESTRICT si controlla subito anche con le chiavi differite: l'albero si scioglie prima di cancellarlo.
            await ExecAsync(conn, tx, "PRAGMA defer_foreign_keys = ON", ct).ConfigureAwait(false);
            // Lapidi e base di prima via per prime: quelle che restano dopo verrebbero dallo svuotamento (non ce ne sono).
            await ExecAsync(conn, tx, "DELETE FROM \"sync_tombstone\"", ct).ConfigureAwait(false);
            await ExecAsync(conn, tx, "DELETE FROM \"sync_base\"", ct).ConfigureAwait(false);
            await ExecAsync(conn, tx, "UPDATE \"habit_nodes\" SET \"parent_id\" = NULL", ct).ConfigureAwait(false);
            foreach (var entity in SyncCatalog.Entities.Reverse())   // figlie prima delle madri
            {
                if (entity.PublicName == "app_setting")
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
#pragma warning disable CA2100 // nomi dei parametri generati qui, valori come parametri
                    cmd.CommandText = $"DELETE FROM \"app_settings\" WHERE \"key\" IN ({KeyParameters(cmd, SyncCatalog.TravellingSettingKeys)})";
#pragma warning restore CA2100
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    continue;
                }

                await ExecAsync(conn, tx, $"DELETE FROM \"{entity.Table}\"", ct).ConfigureAwait(false);
            }

            await efTx.CommitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            state.Applying = false;
        }
    }

    private static async Task ExecAsync(SqliteConnection conn, SqliteTransaction tx, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
#pragma warning disable CA2100 // SQL fisso o con nomi di tabella dal catalogo congelato
        cmd.CommandText = sql;
#pragma warning restore CA2100
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Sync: merging the file '{File}' failed; the other files go on")]
    private static partial void LogMergeFailed(ILogger logger, string file, Exception exception);
}
