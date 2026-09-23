using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;

namespace Recordum.Data.Security;

/// <summary>Decide come si apre il database e lo apre davvero prima dell'avvio.
/// <list type="bullet">
/// <item><b>Windows</b>: DPAPI, nessuna interazione — identico a WPF, stessa cartella, stesso DB.</item>
/// <item><b>Dove esiste <c>db.salt</c> o non c'è DPAPI</b>: passphrase (Argon2id), dietro un
/// <see cref="CachingKeyProvider"/>. Il primo avvio la crea (salt casuale da 16 byte, permessi 600
/// su Unix), i successivi la chiedono.</item>
/// <item>Un database con <c>db.key</c> e senza <c>db.salt</c> dove DPAPI non c'è è
/// <see cref="DbUnlockState.Unsupported"/>: la chiave è un blob DPAPI che questo sistema non sa
/// aprire. Il ponte è il ripristino di un <c>.recbak</c>.</item>
/// </list>
/// <para><b>Passphrase errata</b> si riconosce con una <i>lettura esplicita</i>
/// (<c>SELECT count(*) FROM sqlite_master</c>, <c>SqliteException</c> codice 26 "file is not a
/// database"), non con l'apertura: <c>PRAGMA key</c> non verifica nulla (misurato — l'apertura riesce
/// con una chiave casuale, e il commento in <c>ApplicationInitializer</c> che dice il contrario è
/// falso). E <c>ClearAllPools()</c> fra i tentativi: una connessione già chiavata che il pool
/// restituisce ignora una chiave sbagliata successiva, e la lettura "riuscirebbe".</para></summary>
public sealed class DbUnlockService : IDbUnlockService
{
    private const int SaltLengthBytes = 16;
    private const int SqliteNotADatabase = 26;

    private readonly IDbStoragePaths _paths;
    private readonly Func<IDbKeyProvider>? _platformKey;

    /// <summary>Con la chiave di piattaforma del sistema: DPAPI su Windows, nessuna altrove.</summary>
    public DbUnlockService(IDbStoragePaths paths) : this(paths, DefaultPlatformKey(paths))
    {
    }

    /// <param name="platformKeyFactory">La chiave che custodisce il sistema, o <c>null</c> se questo
    /// sistema non ne ha. Esplicita perché così i test la fingono su Linux (e provano il ramo Windows).</param>
    public DbUnlockService(IDbStoragePaths paths, Func<IDbKeyProvider>? platformKeyFactory)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
        _platformKey = platformKeyFactory;
    }

    private static Func<IDbKeyProvider>? DefaultPlatformKey(IDbStoragePaths paths)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var keyFile = paths.KeyFilePath;
        return () => CreateDpapi(keyFile);
    }

    // La guardia sta QUI e non nel lambda che la chiama: l'analizzatore CA1416 non porta una guardia
    // attraverso il confine di un lambda, ma la riconosce dentro il metodo che costruisce il provider.
#pragma warning disable CA1859 // il tipo di ritorno resta l'interfaccia: DpapiKeyProvider è annotato windows-only
    private static IDbKeyProvider CreateDpapi(string keyFile)
#pragma warning restore CA1859
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI esiste solo su Windows.");
        return new DpapiKeyProvider(keyFile);
    }

    public DbUnlockState DetectState()
    {
        var hasSalt = File.Exists(_paths.SaltFilePath);
        var hasPlatform = _platformKey is not null;

        if (HasDatabase())
        {
            if (hasSalt) return DbUnlockState.NeedsPassphrase;
            // Un database senza salt è cifrato con una chiave di piattaforma (db.key) — o con una che
            // non c'è più. Dove il sistema la custodisce, la si usa; altrove non c'è nulla da fare.
            return hasPlatform ? DbUnlockState.UsePlatformKey : DbUnlockState.Unsupported;
        }

        // Nessun database (un file da 0 byte non lo è). Un salt orfano è una creazione interrotta:
        // nessun dato dipende da lui, si rifà.
        if (hasSalt) return DbUnlockState.NeedsCreation;
        return hasPlatform ? DbUnlockState.UsePlatformKey : DbUnlockState.NeedsCreation;
    }

    public async Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        try
        {
            return state switch
            {
                DbUnlockState.UsePlatformKey => await UnlockWithPlatformKeyAsync(ct).ConfigureAwait(false),
                DbUnlockState.NeedsCreation => await CreateAsync(passphrase, ct).ConfigureAwait(false),
                DbUnlockState.NeedsPassphrase => await EnterAsync(passphrase, ct).ConfigureAwait(false),
                _ => new UnlockResult(UnlockOutcome.Failed, null),
            };
        }
        catch (Exception ex) when (ex is KeyUnavailableException or IOException or UnauthorizedAccessException
                                       or SqliteException or CryptographicException)
        {
            // Tutto ciò che un disco, un permesso o un salt rovinato possono dare: "non riuscito", non
            // "passphrase errata". Dirlo all'utente sbagliato lo manderebbe a riprovare all'infinito.
            SqliteConnection.ClearAllPools();
            return new UnlockResult(UnlockOutcome.Failed, null);
        }
    }

    private async Task<UnlockResult> UnlockWithPlatformKeyAsync(CancellationToken ct)
    {
        if (_platformKey is null) return new UnlockResult(UnlockOutcome.Failed, null);
        var provider = _platformKey();
        using var key = await provider.GetKeyAsync(ct).ConfigureAwait(false);
        // Una chiave di piattaforma che non legge il DB non è "passphrase errata": non c'è nessuna
        // passphrase da correggere.
        if (HasDatabase() && !CanRead(key)) return new UnlockResult(UnlockOutcome.Failed, null);
        return new UnlockResult(UnlockOutcome.Unlocked, provider);
    }

    private async Task<UnlockResult> CreateAsync(string passphrase, CancellationToken ct)
    {
        if (IsTooShort(passphrase)) return new UnlockResult(UnlockOutcome.PassphraseTooShort, null);
        // Riscrivere il salt di un database vero lo renderebbe illeggibile per sempre.
        if (HasDatabase()) return new UnlockResult(UnlockOutcome.Failed, null);

        WriteSalt(_paths.SaltFilePath, RandomNumberGenerator.GetBytes(SaltLengthBytes));
        var provider = new CachingKeyProvider(new PassphraseKeyProvider(passphrase, _paths.SaltFilePath));
        // Deriva ora: il costo di Argon2id si paga qui, una volta, e la cache arriva già calda.
        using (await provider.GetKeyAsync(ct).ConfigureAwait(false)) { }
        return new UnlockResult(UnlockOutcome.Unlocked, provider);
    }

    private async Task<UnlockResult> EnterAsync(string passphrase, CancellationToken ct)
    {
        if (IsTooShort(passphrase)) return new UnlockResult(UnlockOutcome.PassphraseTooShort, null);
        // Open è ReadWriteCreate: con un NeedsPassphrase stantio (il file è sparito) creerebbe un database
        // vuoto e qualunque passphrase di 12+ caratteri "sbloccherebbe". Come CreateAsync, non lo si fa.
        if (!HasDatabase()) return new UnlockResult(UnlockOutcome.Failed, null);

        SqliteConnection.ClearAllPools();   // v. la nota di classe: il pool ignorerebbe la chiave sbagliata
        var provider = new CachingKeyProvider(new PassphraseKeyProvider(passphrase, _paths.SaltFilePath));
        try
        {
            using var key = await provider.GetKeyAsync(ct).ConfigureAwait(false);
            if (CanRead(key)) return new UnlockResult(UnlockOutcome.Unlocked, provider);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
        provider.Dispose();                 // azzera la chiave sbagliata che la cache si era tenuta
        return new UnlockResult(UnlockOutcome.WrongPassphrase, null);
    }

    private static bool IsTooShort(string passphrase) =>
        string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < PassphraseKeyProvider.MinPassphraseLength;

    private bool HasDatabase() =>
        File.Exists(_paths.DatabasePath) && new FileInfo(_paths.DatabasePath).Length > 0;

    /// <summary>La lettura esplicita che dice se la chiave apre il file. <c>false</c> = codice 26
    /// ("file is not a database"); ogni altro errore SQLite risale al chiamante.</summary>
    private bool CanRead(KeyMaterial key)
    {
        try
        {
            using var conn = SqlCipherConnectionFactory.Open(_paths.DatabasePath, key);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master";
            cmd.ExecuteScalar();
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteNotADatabase)
        {
            return false;
        }
        finally
        {
            // Sempre: toglie dal pool la connessione chiavata (giusta o sbagliata) e rilascia il file.
            SqliteConnection.ClearAllPools();
        }
    }

    private static void WriteSalt(string path, byte[] salt)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        // Il file nasce già 600: chmod DOPO la scrittura lascerebbe una finestra in cui è leggibile
        // col umask di default.
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var fs = new FileStream(path, options)) fs.Write(salt);
        // UnixCreateMode vale solo alla creazione: un salt orfano preesistente conserva i suoi permessi.
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
