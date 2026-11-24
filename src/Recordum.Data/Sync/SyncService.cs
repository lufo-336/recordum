using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    Func<string, string, ISyncFileStore>? storeFactory = null,
    string? machineName = null) : ISyncService
{
    public const string FolderSettingKey = "sync.folder";
    public const string PassphraseSettingKey = "sync.passphrase";
    public const string IncludePhotosSettingKey = "sync.include_photos";
    public const string LastRunMsSettingKey = "sync.last_run_ms";
    public const string JoinedSettingKey = "sync.joined";
    public const string LastWrittenSignatureSettingKey = "sync.last_written_signature";
    public const string LastWrittenHashSettingKey = "sync.last_written_hash";
    public const string PendingWrittenHashSettingKey = "sync.pending_written_hash";
    /// <summary>JSON: per ogni pari, il written_at_ms dell'ultimo suo file unito qui (va nel manifest, Ruling 17).</summary>
    public const string MergedFromSettingKey = "sync.merged_from";

    private const long SignatureModulus = 1_000_000_007;

    private static readonly SyncRunResult NotRun = new(Ran: false, OwnFileWritten: false, Peers: []);

    private readonly ILogger _logger = logger ?? NullLogger<SyncService>.Instance;

    // Il nome della macchina, iniettabile solo per i test: un database con un altro nome in sync_meta è stato copiato qui.
    private readonly string _machineName = machineName ?? Environment.MachineName;

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
            // ponytail: il proprio file letto come file della cartella; lo store SAF del telefono vorrà un membro esiste/dimensione.
            var own = new FileInfo(Path.Combine(folder, BuildOwnFileName(id.Name, id.Id)));
            if (own.Exists) ownFileBytes = own.Length;
        }

        return new SyncStatus(lastRun, runState.Peers, await CountTextConflictsAsync(ct).ConfigureAwait(false), ownFileBytes);
    }

    public async Task SetPassphraseAsync(string passphrase, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        await settings.SetAsync(PassphraseSettingKey, passphrase, ct).ConfigureAwait(false);
        // Il proprio file è cifrato con la frase di prima: il giro dopo lo riscrive.
        await settings.SetAsync(LastWrittenSignatureSettingKey, "", ct).ConfigureAwait(false);
    }

    public async Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false))
            || string.IsNullOrWhiteSpace(await settings.GetAsync(PassphraseSettingKey, ct).ConfigureAwait(false)))
            return SyncJoinState.NotConfigured;
        // Sola lettura: un database senza dati dell'utente risulta collegato, e il primo giro lo scrive.
        return await IsJoinedAsync(ct).ConfigureAwait(false) || !await HasUserDataAsync(ct).ConfigureAwait(false)
            ? SyncJoinState.Joined
            : SyncJoinState.NeedsChoice;
    }

    public async Task JoinAsync(SyncJoinChoice choice, CancellationToken ct)
    {
        await runState.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var folder = await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false);
            var passphrase = await settings.GetAsync(PassphraseSettingKey, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(passphrase))
                throw new SyncJoinRefusedException(SyncJoinRefusal.MissingConfiguration, "The folder or the sync passphrase is missing.");

            // "Parti dai dati degli altri" senza un file leggibile (frase sbagliata, cartella vuota) svuoterebbe questo
            // dispositivo senza dargli niente: si rifiuta prima di fare il backup.
            var startFromOthers = choice == SyncJoinChoice.StartFromOthers;
            if (startFromOthers && !await AnyPeerOpensAsync(folder, passphrase, ct).ConfigureAwait(false))
                throw new SyncJoinRefusedException(SyncJoinRefusal.NoReadablePeer, "No file from another device opens in the folder.");

            // Sempre prima un backup (spec §9): se fallisce, il collegamento non va avanti. È protetto dalla frase
            // dell'unione, l'unica che l'utente ha appena scritto e conosce.
            Directory.CreateDirectory(paths.BackupsDirectory);
            var backupPath = Path.Combine(
                paths.BackupsDirectory,
                $"pre-sync-join-{clock.UtcNow.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.recbak");
            // ponytail: registrato come Manual ("Prima del ripristino" ingannerebbe); un tipo suo vuole una migrazione del
            // CHECK ck_backup_log_kind, se gli utenti lo chiedono.
            await backupService.CreateAsync(backupPath, passphrase, new BackupOptions(BackupKind.Manual), ct).ConfigureAwait(false);

            if (!startFromOthers)
                await settings.SetAsync(JoinedSettingKey, "true", ct).ConfigureAwait(false);

            await RunCoreAsync(ct, startFromOthers).ConfigureAwait(false);
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
                .Where(s => s.Key == FolderSettingKey
                         || s.Key == JoinedSettingKey
                         || s.Key == MergedFromSettingKey
                         || s.Key == LastRunMsSettingKey
                         || s.Key == LastWrittenSignatureSettingKey
                         || s.Key == LastWrittenHashSettingKey
                         || s.Key == PendingWrittenHashSettingKey)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            // Anche la via d'uscita da un'identità doppia: ricollegato, il dispositivo rientra come nuovo (nome di file
            // nuovo, nessuna base), come dopo un ripristino.
            await db.SyncMeta.ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.SyncBases.ExecuteDeleteAsync(ct).ConfigureAwait(false);
            deviceIdentity.Invalidate();
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

    private async Task<SyncRunResult> RunCoreAsync(CancellationToken ct, bool startFromOthers = false)
    {
        var folder = await settings.GetAsync(FolderSettingKey, ct).ConfigureAwait(false);
        var passphrase = await settings.GetAsync(PassphraseSettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(passphrase))
            return NotRun;
        if (!startFromOthers && !await IsJoinedAsync(ct).ConfigureAwait(false))
        {
            if (await HasUserDataAsync(ct).ConfigureAwait(false))
                return NotRun;   // aspetta la scelta dell'utente (JoinAsync)
            await settings.SetAsync(JoinedSettingKey, "true", ct).ConfigureAwait(false);   // un database vuoto si collega da solo
        }

        var (deviceId, deviceName) = await EnsureIdentityAsync(ct).ConfigureAwait(false);
        var ownFileName = BuildOwnFileName(deviceName, deviceId);
        var store = _storeFactory(folder, ownFileName);
        // Acceso di partenza (spec §9): spento solo se l'utente l'ha spento.
        var includePhotos = !string.Equals(
            await settings.GetAsync(IncludePhotosSettingKey, ct).ConfigureAwait(false), "false", StringComparison.OrdinalIgnoreCase);

        var peers = new List<SyncPeerStatus>();

        // Un proprio file diverso da quello scritto qui l'ha scritto un altro con la stessa identità (un database copiato
        // su una macchina con lo stesso nome): non si sovrascrive e lo stato lo dice.
        // ponytail: il proprio file letto come file della cartella; lo store SAF del telefono vorrà un membro esiste/dimensione.
        var ownPath = Path.Combine(folder, ownFileName);
        var ownFileIsForeign = await OwnFileIsForeignAsync(ownPath, ct).ConfigureAwait(false);
        if (ownFileIsForeign)
            peers.Add(new SyncPeerStatus(deviceName, File.GetLastWriteTimeUtc(ownPath), SyncPeerProblem.SameIdentity));
        var merged = false;
        var mergedAnyPeer = false;
        var mergedFrom = await ReadMergedFromAsync(ct).ConfigureAwait(false);
        var mergedFromChanged = false;
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
                    var emptyFirst = startFromOthers && !mergedAnyPeer;
                    var report = await SyncMerger.MergeAsync(
                        db, snapshot, deviceId, includePhotos, clock, afterEntity: null, emptyTravellingFirst: emptyFirst, ct).ConfigureAwait(false);
                    if (emptyFirst)
                    {
                        mergedFrom.Clear();
                        mergedFromChanged = false;
                    }
                    mergedAnyPeer = true;
                    merged |= emptyFirst || report.Inserted + report.Updated + report.Deleted + report.Repaired + report.Rescued > 0;
                    // Solo dopo il commit: se si perde (arresto prima del salvataggio) il pari crede che non abbiamo visto,
                    // il lato sicuro.
                    var peerId = snapshot.Manifest.DeviceId;
                    if (!mergedFrom.TryGetValue(peerId, out var seen) || seen < snapshot.Manifest.WrittenAtMs)
                    {
                        mergedFrom[peerId] = snapshot.Manifest.WrittenAtMs;
                        mergedFromChanged = true;
                    }
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

        if (startFromOthers)
        {
            if (!mergedAnyPeer)
            {
                runState.SetPeers(peers);
                throw new SyncJoinRefusedException(SyncJoinRefusal.NoReadablePeer, "No file from another device could be merged.");
            }

            await settings.SetAsync(JoinedSettingKey, "true", ct).ConfigureAwait(false);
        }

        if (mergedFromChanged)
            await settings.SetAsync(MergedFromSettingKey, JsonSerializer.Serialize(mergedFrom), ct).ConfigureAwait(false);

        // merged_from non entra nella firma: se un suo cambio riscrivesse il file, due dispositivi si riscriverebbero a
        // vicenda a ogni giro. Viaggia con la prossima scrittura vera, che è proprio quella che serve al pari (una
        // pulizia, una cancellazione).
        var ownFileWritten = !ownFileIsForeign && await WriteOwnFileIfChangedAsync(
            store, ownPath, passphrase, deviceId, deviceName, includePhotos, merged, mergedFrom, ct).ConfigureAwait(false);

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
        bool merged, IReadOnlyDictionary<string, long> mergedFrom, CancellationToken ct)
    {
        var signature = await ComputeSignatureAsync(includePhotos, ct).ConfigureAwait(false);
        var lastSignature = await settings.GetAsync(LastWrittenSignatureSettingKey, ct).ConfigureAwait(false);
        if (!merged && File.Exists(ownPath) && string.Equals(signature, lastSignature, StringComparison.Ordinal))
            return false;

        var tempZip = NewTempPath("out");
        var tempSealed = NewTempPath("sealed");
        string pendingHash;
        try
        {
            await using (var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
            await using (var zip = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var options = new SyncWriteOptions(deviceId, deviceName, appVersion, includePhotos, clock.UtcNowMs, mergedFrom);
                await SyncArchiveWriter.WriteAsync(db, zip, options, ct).ConfigureAwait(false);
            }

            // Cifrato prima su un temporaneo: la sua impronta si salva come "in sospeso" prima della rinomina, così un
            // arresto fra la rinomina e la promozione non fa sembrare estraneo il file appena scritto.
            await using (var zip = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var sealedFile = new FileStream(tempSealed, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await cipher.EncryptAsync(zip, sealedFile, passphrase, ct).ConfigureAwait(false);
            pendingHash = (await HashOfAsync(tempSealed, ct).ConfigureAwait(false))!;
            await settings.SetAsync(PendingWrittenHashSettingKey, pendingHash, ct).ConfigureAwait(false);

            await store.WriteOwnAsync(async target =>
            {
                await using var sealedFile = new FileStream(tempSealed, FileMode.Open, FileAccess.Read, FileShare.Read);
                await sealedFile.CopyToAsync(target, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            SecureWipe.DeleteFile(tempZip);
            SecureWipe.DeleteFile(tempSealed);
        }

        await PromoteHashAsync(pendingHash, ct).ConfigureAwait(false);
        await settings.SetAsync(LastWrittenSignatureSettingKey, signature, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Vero se il proprio file nella cartella non l'ha scritto questo dispositivo: un altro con la stessa
    /// identità (un database copiato su una macchina con lo stesso nome).</summary>
    private async Task<bool> OwnFileIsForeignAsync(string ownPath, CancellationToken ct)
    {
        var ownHash = await HashOfAsync(ownPath, ct).ConfigureAwait(false);
        if (ownHash is null) return false;
        var last = await settings.GetAsync(LastWrittenHashSettingKey, ct).ConfigureAwait(false);
        if (string.Equals(ownHash, last, StringComparison.Ordinal)) return false;

        var pending = await settings.GetAsync(PendingWrittenHashSettingKey, ct).ConfigureAwait(false);
        // Scritto qui, poi un arresto prima della promozione; oppure nessuna impronta (impostazioni perse, il primo giro
        // di un database copiato): si adotta.
        // ponytail: senza impronta un clone vero non si distingue; se ne accorge l'altro, al suo giro dopo.
        if (string.Equals(ownHash, pending, StringComparison.Ordinal) || (string.IsNullOrEmpty(last) && string.IsNullOrEmpty(pending)))
        {
            await PromoteHashAsync(ownHash, ct).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async Task PromoteHashAsync(string hash, CancellationToken ct)
    {
        await settings.SetAsync(LastWrittenHashSettingKey, hash, ct).ConfigureAwait(false);
        await settings.SetAsync(PendingWrittenHashSettingKey, "", ct).ConfigureAwait(false);
    }

    // ponytail: legge per intero il proprio file a ogni giro; con file da centinaia di MB, prima un confronto di dimensione e data.
    private static async Task<string?> HashOfAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(await SHA256.HashDataAsync(file, ct).ConfigureAwait(false));
    }

    private string NewTempPath(string kind)
    {
        Directory.CreateDirectory(paths.TempDirectory);
        return Path.Combine(paths.TempDirectory, $"recordum-sync-{kind}-{Guid.NewGuid():N}.zip");
    }

    private async Task<Dictionary<string, long>> ReadMergedFromAsync(CancellationToken ct)
    {
        var json = await settings.GetAsync(MergedFromSettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, long>(StringComparer.Ordinal);
        try
        {
            return new Dictionary<string, long>(
                JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? [], StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Un valore rovinato vale "non abbiamo visto niente": il lato sicuro.
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }
    }

    private async Task<bool> IsJoinedAsync(CancellationToken ct) =>
        string.Equals(await settings.GetAsync(JoinedSettingKey, ct).ConfigureAwait(false), "true", StringComparison.Ordinal);

    private async Task<bool> HasUserDataAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.HabitNodes.IgnoreQueryFilters().AnyAsync(ct).ConfigureAwait(false)
            || await db.Notes.AnyAsync(ct).ConfigureAwait(false);
    }

    private async Task<(string Id, string Name)?> ReadIdentityAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var meta = await db.SyncMeta.AsNoTracking().FirstOrDefaultAsync(ct).ConfigureAwait(false);
        // Un nome diverso da quello della macchina: il database è stato copiato qui, l'identità non è più sua.
        if (meta is null || string.IsNullOrWhiteSpace(meta.DeviceId) || meta.DeviceName != _machineName)
            return null;
        return (meta.DeviceId, meta.DeviceName);
    }

    /// <summary>L'identità alla prima richiesta, dopo un ripristino (che la cancella) e su una macchina con un altro nome
    /// (un database copiato): GUID "D" maiuscolo e il nome della macchina; la base di prima va via come dopo un
    /// ripristino (spec §8.4-bis). Si chiama solo sotto il cancello, così due giri non creano due righe.</summary>
    private async Task<(string Id, string Name)> EnsureIdentityAsync(CancellationToken ct)
    {
        if (await ReadIdentityAsync(ct).ConfigureAwait(false) is { } existing)
            return existing;

        var meta = new SyncMeta
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid().ToString("D").ToUpperInvariant(),
            DeviceName = _machineName,
            CreatedAt = clock.UtcNow.UtcDateTime,
        };
        await using (var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            await db.SyncMeta.ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.SyncBases.ExecuteDeleteAsync(ct).ConfigureAwait(false);
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
        var (fmsCount, fmsMax, fmsSum) = await AggregateAsync(conn, "sync_field_ms", "ms", null, ct).ConfigureAwait(false);
        var aliasSig = await AliasSignatureAsync(conn, ct).ConfigureAwait(false);
        var photoSig = await PhotoSignatureAsync(conn, includePhotos, ct).ConfigureAwait(false);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rows}:{max}:{sum}:{tombs}:{tombMax}:{tombSum}:{fmsCount}:{fmsMax}:{fmsSum}:{aliasSig}:{photoSig}:{includePhotos}");
    }

    private static async Task<string> AliasSignatureAsync(SqliteConnection conn, CancellationToken ct)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"entity\", \"entity_id\", \"alias_of\" FROM \"sync_tombstone\" WHERE \"alias_of\" IS NOT NULL ORDER BY \"entity\", \"entity_id\"";
        long count = 0;
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            count++;
            hasher.AppendData(Encoding.UTF8.GetBytes($"{reader.GetString(0)}\0{reader.GetString(1)}\0{reader.GetString(2)}\n"));
        }

        return string.Create(CultureInfo.InvariantCulture, $"{count}:{Convert.ToHexStringLower(hasher.GetHashAndReset())}");
    }

    // Lunghezze, non byte: la firma gira a ogni giro e leggere ogni foto costerebbe GB. length() di SQLite non carica il blob.
    private static async Task<string> PhotoSignatureAsync(SqliteConnection conn, bool includePhotos, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = includePhotos
            ? "SELECT COALESCE(SUM(length(\"thumbnail\")), 0), COUNT(\"content\"), COALESCE(SUM(length(\"content\")), 0) FROM \"attachment\""
            : "SELECT COALESCE(SUM(length(\"thumbnail\")), 0), 0, 0 FROM \"attachment\"";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await reader.ReadAsync(ct).ConfigureAwait(false);
        return string.Create(CultureInfo.InvariantCulture, $"{reader.GetInt64(0)}/{reader.GetInt64(1)}/{reader.GetInt64(2)}");
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Sync: merging the file '{File}' failed; the other files go on")]
    private static partial void LogMergeFailed(ILogger logger, string file, Exception exception);
}
