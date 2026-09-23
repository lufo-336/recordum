using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>Le connessioni SQLite sono in pool e questi test chiamano <c>ClearAllPools()</c> (come fa
/// il servizio): girano da soli, mai in parallelo agli altri test che tengono connessioni aperte.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlCipherPoolCollection
{
    public const string Name = "SQLCipher pool";
}

/// <summary>Su SQLCipher VERO, in una cartella temporanea, con la passphrase vera (Argon2id, ~0,8 s a
/// derivazione): niente doppi per il pezzo che decide se l'utente entra nei propri dati.</summary>
[Collection(SqlCipherPoolCollection.Name)]
public sealed class DbUnlockServiceTests : IDisposable
{
    private const string Passphrase = "correct horse battery staple";
    private const string Wrong = "incorrect horse battery staple";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-unlock-" + Guid.NewGuid().ToString("N"));
    private readonly TestPaths _paths;

    public DbUnlockServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _paths = new TestPaths(Path.Combine(_dir, "recordum.db"), Path.Combine(_dir, "db.key"), Path.Combine(_dir, "db.salt"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    private sealed record TestPaths(string DatabasePath, string KeyFilePath, string SaltFilePath) : IDbStoragePaths;

    /// <summary>Un servizio "senza chiave di piattaforma" (Linux) o con una finta (Windows).</summary>
    private DbUnlockService Linux() => new(_paths, platformKeyFactory: null);

    private DbUnlockService Windows(IDbKeyProvider? key = null) =>
        new(_paths, () => key ?? new FixedKey(Bytes(7)));

    private static byte[] Bytes(byte seed) => Enumerable.Range(0, KeyMaterial.LengthBytes).Select(i => (byte)(seed + i)).ToArray();

    private sealed class FixedKey(byte[] key) : IDbKeyProvider
    {
        public string ProviderId => "fixed";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new KeyMaterial((byte[])key.Clone()));
    }

    /// <summary>Scrive davvero una tabella cifrata con la chiave del provider: da lì in poi il file è un
    /// database non vuoto, e una chiave sbagliata non può più "riuscire" su un file vuoto.</summary>
    private static async Task SeedAsync(string dbPath, IDbKeyProvider provider)
    {
        using var key = await provider.GetKeyAsync();
        using (var conn = SqlCipherConnectionFactory.Open(dbPath, key))
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (42);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
    }

    private static async Task<long> ReadAsync(string dbPath, IDbKeyProvider provider)
    {
        using var key = await provider.GetKeyAsync();
        using var conn = SqlCipherConnectionFactory.Open(dbPath, key);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT x FROM t";
        return (long)cmd.ExecuteScalar()!;
    }

    [Theory]
    [InlineData(false, false, false, false, DbUnlockState.NeedsCreation)]    // Linux, primo avvio
    [InlineData(false, false, false, true, DbUnlockState.UsePlatformKey)]    // Windows, primo avvio: DPAPI crea la chiave
    [InlineData(false, true, false, false, DbUnlockState.NeedsCreation)]     // salt orfano (creazione interrotta): si rifà
    [InlineData(false, true, false, true, DbUnlockState.NeedsCreation)]
    [InlineData(true, true, false, false, DbUnlockState.NeedsPassphrase)]    // Linux, avvio successivo
    [InlineData(true, true, false, true, DbUnlockState.NeedsPassphrase)]     // Windows con passphrase scelta (KeyTransitionService)
    [InlineData(true, false, true, true, DbUnlockState.UsePlatformKey)]      // Windows classico: db + db.key
    [InlineData(true, false, true, false, DbUnlockState.Unsupported)]        // DB Windows aperto su Linux
    [InlineData(true, false, false, false, DbUnlockState.Unsupported)]       // DB senza alcun materiale di chiave: non lo si può aprire
    [InlineData(true, false, false, true, DbUnlockState.UsePlatformKey)]
    public void DetectState_reads_the_files_on_disk(bool db, bool salt, bool keyFile, bool platform, DbUnlockState expected)
    {
        if (db) File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        if (salt) File.WriteAllBytes(_paths.SaltFilePath, new byte[16]);
        if (keyFile) File.WriteAllBytes(_paths.KeyFilePath, [9]);

        var sut = platform ? Windows() : Linux();

        Assert.Equal(expected, sut.DetectState());
    }

    [Fact]
    public void An_empty_database_file_counts_as_no_database()
    {
        // Una connessione che si apre e chiude senza scrivere lascia un file da 0 byte: NON è un
        // database cifrato con una chiave incognita. Su un file così qualunque chiave "riesce".
        File.WriteAllBytes(_paths.DatabasePath, []);
        File.WriteAllBytes(_paths.SaltFilePath, new byte[16]);

        Assert.Equal(DbUnlockState.NeedsCreation, Linux().DetectState());
    }

    [Fact]
    public async Task Creation_writes_a_16_byte_salt_and_yields_a_working_provider()
    {
        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);

        Assert.Equal(UnlockOutcome.Unlocked, result.Outcome);
        Assert.NotNull(result.Provider);
        Assert.Equal(KeyProviderId.Passphrase, result.Provider!.ProviderId);
        Assert.Equal(16, new FileInfo(_paths.SaltFilePath).Length);

        // Il provider restituito è quello che aprirà il DB: prova che scrive e rilegge.
        await SeedAsync(_paths.DatabasePath, result.Provider);
        Assert.Equal(42, await ReadAsync(_paths.DatabasePath, result.Provider));
    }

    [Fact]
    public async Task The_next_start_unlocks_with_the_same_passphrase_and_rejects_a_wrong_one()
    {
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);
        await SeedAsync(_paths.DatabasePath, created.Provider!);

        var next = Linux();
        Assert.Equal(DbUnlockState.NeedsPassphrase, next.DetectState());

        var wrong = await next.UnlockAsync(DbUnlockState.NeedsPassphrase, Wrong);
        Assert.Equal(UnlockOutcome.WrongPassphrase, wrong.Outcome);
        Assert.Null(wrong.Provider);

        var right = await next.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase);
        Assert.Equal(UnlockOutcome.Unlocked, right.Outcome);
        Assert.Equal(42, await ReadAsync(_paths.DatabasePath, right.Provider!));
    }

    [Fact]
    public async Task A_wrong_passphrase_after_a_correct_one_in_the_same_process_is_still_rejected()
    {
        // Il pool di Microsoft.Data.Sqlite restituisce una connessione GIÀ chiavata: senza
        // ClearAllPools() fra i tentativi, la chiave sbagliata viene ignorata e la lettura "riesce".
        // (Misurato: la prima stesura di questo controllo diceva "read succeeded ?!".)
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);
        await SeedAsync(_paths.DatabasePath, created.Provider!);
        var sut = Linux();

        var right = await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase);
        var wrong = await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Wrong);

        Assert.Equal(UnlockOutcome.Unlocked, right.Outcome);
        Assert.Equal(UnlockOutcome.WrongPassphrase, wrong.Outcome);
    }

    [Fact]
    public async Task A_wrong_passphrase_is_rejected_even_when_a_pooled_connection_was_keyed_with_the_right_one()
    {
        // Una connessione già chiavata giusta (l'interceptor di EF, un secondo sblocco nello stesso
        // processo) resta nel pool: Open() applica la chiave con PRAGMA key DOPO l'apertura, quindi il
        // pool la restituirebbe ancora sbloccata. Solo il ClearAllPools() in testa a EnterAsync lo evita
        // (quello nel finally di CanRead ripulisce soltanto ciò che CanRead ha aperto lui).
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);
        await SeedAsync(_paths.DatabasePath, created.Provider!);
        using (var key = await created.Provider!.GetKeyAsync())
        using (var conn = SqlCipherConnectionFactory.Open(_paths.DatabasePath, key))
        {
            // Una lettura vera: senza, la connessione non ha ancora toccato il file e il pool non la
            // trattiene "già chiavata" (misurato). Poi dispose SENZA ClearAllPools(): torna nel pool.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master";
            cmd.ExecuteScalar();
        }

        var wrong = await Linux().UnlockAsync(DbUnlockState.NeedsPassphrase, Wrong);

        Assert.Equal(UnlockOutcome.WrongPassphrase, wrong.Outcome);
    }

    [Fact]
    public async Task Entering_a_passphrase_with_no_database_fails_and_creates_nothing()
    {
        // Uno stato NeedsPassphrase stantio (il file è sparito): Open() è ReadWriteCreate, quindi senza
        // guardia creerebbe un DB vuoto e QUALUNQUE passphrase di 12+ caratteri "sbloccherebbe".
        await File.WriteAllBytesAsync(_paths.SaltFilePath, RandomNumberGenerator16());

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        Assert.Null(result.Provider);
        Assert.False(File.Exists(_paths.DatabasePath));
    }

    private static byte[] RandomNumberGenerator16() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("elevenchars")]          // 11: il confine
    [InlineData("            ")]         // 12 spazi: lunga abbastanza ma non è una passphrase
    public async Task A_too_short_passphrase_is_refused_without_side_effects(string passphrase)
    {
        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, passphrase);

        Assert.Equal(UnlockOutcome.PassphraseTooShort, result.Outcome);
        Assert.Null(result.Provider);
        Assert.False(File.Exists(_paths.SaltFilePath));      // niente salt scritto
    }

    [Fact]
    public async Task Twelve_characters_is_enough()
    {
        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, "twelve-chars");

        Assert.Equal(UnlockOutcome.Unlocked, result.Outcome);
    }

    [Fact]
    public async Task Creation_refuses_to_overwrite_the_salt_of_an_existing_database()
    {
        // Se uno stato NeedsCreation stantio arrivasse qui con un DB vero già presente, riscrivere il
        // salt renderebbe quel DB illeggibile per sempre.
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);
        await SeedAsync(_paths.DatabasePath, created.Provider!);
        var saltBefore = await File.ReadAllBytesAsync(_paths.SaltFilePath);

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, "another passphrase!");

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        Assert.Equal(saltBefore, await File.ReadAllBytesAsync(_paths.SaltFilePath));
    }

    [Fact]
    public async Task An_orphan_salt_with_no_database_is_replaced()
    {
        await File.WriteAllBytesAsync(_paths.SaltFilePath, new byte[16]);           // sedici zeri

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);

        Assert.Equal(UnlockOutcome.Unlocked, result.Outcome);
        Assert.NotEqual(new byte[16], await File.ReadAllBytesAsync(_paths.SaltFilePath));
    }

    [Fact]
    public async Task The_salt_is_owner_only_on_unix_even_when_it_replaces_a_looser_file()
    {
        if (OperatingSystem.IsWindows()) return;                                    // ACL, non modi Unix
        await File.WriteAllBytesAsync(_paths.SaltFilePath, new byte[16]);
        File.SetUnixFileMode(_paths.SaltFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_paths.SaltFilePath));
    }

    [Fact]
    public async Task A_fresh_salt_is_created_owner_only()
    {
        if (OperatingSystem.IsWindows()) return;

        await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_paths.SaltFilePath));
    }

    [Fact]
    public async Task A_corrupt_salt_is_a_failure_not_a_wrong_passphrase()
    {
        // Diciamo all'utente "passphrase errata" solo quando lo è davvero: un salt di lunghezza
        // sbagliata non si risolve riprovando.
        File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        await File.WriteAllBytesAsync(_paths.SaltFilePath, new byte[5]);

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Unsupported_is_a_failure_and_touches_nothing()
    {
        File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        File.WriteAllBytes(_paths.KeyFilePath, [9]);

        var result = await Linux().UnlockAsync(DbUnlockState.Unsupported, Passphrase);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        Assert.False(File.Exists(_paths.SaltFilePath));
    }

    [Fact]
    public async Task The_platform_key_path_needs_no_passphrase()
    {
        var key = new FixedKey(Bytes(7));
        var sut = Windows(key);

        var result = await sut.UnlockAsync(DbUnlockState.UsePlatformKey, "");

        Assert.Equal(UnlockOutcome.Unlocked, result.Outcome);
        Assert.Same(key, result.Provider);
    }

    [Fact]
    public async Task The_platform_key_that_cannot_read_the_database_is_a_failure_not_a_wrong_passphrase()
    {
        await SeedAsync(_paths.DatabasePath, new FixedKey(Bytes(7)));

        var result = await Windows(new FixedKey(Bytes(99))).UnlockAsync(DbUnlockState.UsePlatformKey, "");

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task UsePlatformKey_without_a_platform_key_is_a_failure()
    {
        var result = await Linux().UnlockAsync(DbUnlockState.UsePlatformKey, "");

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void The_default_constructor_offers_a_platform_key_only_on_windows()
    {
        var sut = new DbUnlockService(_paths);

        // Su Linux e macOS il costruttore di default NON ha una chiave di piattaforma: primo avvio =
        // NeedsCreation. Su Windows = UsePlatformKey (DPAPI, come WPF).
        Assert.Equal(OperatingSystem.IsWindows() ? DbUnlockState.UsePlatformKey : DbUnlockState.NeedsCreation, sut.DetectState());
    }
}
