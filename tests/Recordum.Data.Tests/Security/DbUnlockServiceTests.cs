using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
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
    private DbUnlockService Linux() => new(_paths, platformKeyFactory: null, new NullSecretStore());

    private DbUnlockService Windows(IDbKeyProvider? key = null) =>
        new(_paths, () => key ?? new FixedKey(Bytes(7)), new NullSecretStore());

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
        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);

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
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
        await SeedAsync(_paths.DatabasePath, created.Provider!);

        var next = Linux();
        Assert.Equal(DbUnlockState.NeedsPassphrase, next.DetectState());

        var wrong = await next.UnlockAsync(DbUnlockState.NeedsPassphrase, Wrong, rememberOnDevice: false);
        Assert.Equal(UnlockOutcome.WrongPassphrase, wrong.Outcome);
        Assert.Null(wrong.Provider);

        var right = await next.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false);
        Assert.Equal(UnlockOutcome.Unlocked, right.Outcome);
        Assert.Equal(42, await ReadAsync(_paths.DatabasePath, right.Provider!));
    }

    [Fact]
    public async Task A_wrong_passphrase_after_a_correct_one_in_the_same_process_is_still_rejected()
    {
        // Il pool di Microsoft.Data.Sqlite restituisce una connessione GIÀ chiavata: senza
        // ClearAllPools() fra i tentativi, la chiave sbagliata viene ignorata e la lettura "riesce".
        // (Misurato: la prima stesura di questo controllo diceva "read succeeded ?!".)
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
        await SeedAsync(_paths.DatabasePath, created.Provider!);
        var sut = Linux();

        var right = await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false);
        var wrong = await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Wrong, rememberOnDevice: false);

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
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
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

        var wrong = await Linux().UnlockAsync(DbUnlockState.NeedsPassphrase, Wrong, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.WrongPassphrase, wrong.Outcome);
    }

    [Fact]
    public async Task Entering_a_passphrase_with_no_database_fails_and_creates_nothing()
    {
        // Uno stato NeedsPassphrase stantio (il file è sparito): Open() è ReadWriteCreate, quindi senza
        // guardia creerebbe un DB vuoto e QUALUNQUE passphrase di 12+ caratteri "sbloccherebbe".
        await File.WriteAllBytesAsync(_paths.SaltFilePath, RandomNumberGenerator16());

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false);

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
        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, passphrase, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.PassphraseTooShort, result.Outcome);
        Assert.Null(result.Provider);
        Assert.False(File.Exists(_paths.SaltFilePath));      // niente salt scritto
    }

    [Fact]
    public async Task Twelve_characters_is_enough()
    {
        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, "twelve-chars", rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Unlocked, result.Outcome);
    }

    [Fact]
    public async Task Creation_refuses_to_overwrite_the_salt_of_an_existing_database()
    {
        // Se uno stato NeedsCreation stantio arrivasse qui con un DB vero già presente, riscrivere il
        // salt renderebbe quel DB illeggibile per sempre.
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
        await SeedAsync(_paths.DatabasePath, created.Provider!);
        var saltBefore = await File.ReadAllBytesAsync(_paths.SaltFilePath);

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, "another passphrase!", rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        Assert.Equal(saltBefore, await File.ReadAllBytesAsync(_paths.SaltFilePath));
    }

    [Fact]
    public async Task An_orphan_salt_is_set_aside_never_replaced()
    {
        // Decisione A del Passo 0 (2026-09-25): un salt senza database si sposta di lato, non si tronca mai.
        // Se il suo database esiste da qualche altra parte, quel salt è l'unico modo di riaprirlo.
        var zeros = new byte[16];
        await File.WriteAllBytesAsync(_paths.SaltFilePath, zeros);
        var sut = Linux();

        Assert.Equal(OrphanReconciliation.SaltSetAside, sut.ReconcileOrphans());

        var aside = Assert.Single(Directory.GetFiles(_dir, "db.salt.orphan-*"));
        Assert.Equal(zeros, await File.ReadAllBytesAsync(aside));
        Assert.Equal(DbUnlockState.NeedsCreation, sut.DetectState());
        Assert.Equal(UnlockOutcome.Unlocked, (await sut.UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false)).Outcome);
        Assert.NotEqual(zeros, await File.ReadAllBytesAsync(_paths.SaltFilePath));
    }

    [Fact]
    public async Task A_restore_interrupted_between_its_two_moves_gets_the_database_back()
    {
        var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
        await SeedAsync(_paths.DatabasePath, created.Provider!);
        SqliteConnection.ClearAllPools();
        File.Move(_paths.DatabasePath, _paths.DatabasePath + ".old");   // RestoreService muore fra le due File.Move
        var sut = Linux();

        Assert.Equal(OrphanReconciliation.RecoveredInterruptedRestore, sut.ReconcileOrphans());

        Assert.Equal(DbUnlockState.NeedsPassphrase, sut.DetectState());
        Assert.Equal(UnlockOutcome.Unlocked, (await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false)).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task With_nothing_orphaned_reconciliation_touches_nothing(bool withDatabase)
    {
        if (withDatabase)
        {
            var created = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
            await SeedAsync(_paths.DatabasePath, created.Provider!);
        }
        var before = Directory.GetFiles(_dir).Order().ToArray();

        Assert.Equal(OrphanReconciliation.None, Linux().ReconcileOrphans());
        Assert.Equal(before, Directory.GetFiles(_dir).Order().ToArray());
    }

    [Fact]
    public async Task Creation_never_truncates_a_salt_that_appeared_meanwhile()
    {
        // La corsa fra due istanze al primo avvio: la seconda arriva qui con NeedsCreation quando la prima ha
        // già scritto il suo salt. Troncarlo renderebbe illeggibile il database che la prima sta creando.
        var theirs = RandomNumberGenerator.GetBytes(16);
        await File.WriteAllBytesAsync(_paths.SaltFilePath, theirs);

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        Assert.Equal(theirs, await File.ReadAllBytesAsync(_paths.SaltFilePath));
    }

    [Fact]
    public async Task A_looser_orphan_is_set_aside_and_the_new_salt_is_owner_only()
    {
        if (OperatingSystem.IsWindows()) return;                                    // ACL, non modi Unix
        await File.WriteAllBytesAsync(_paths.SaltFilePath, new byte[16]);
        File.SetUnixFileMode(_paths.SaltFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var sut = Linux();

        sut.ReconcileOrphans();
        await sut.UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_paths.SaltFilePath));
    }

    [Fact]
    public async Task A_fresh_salt_is_created_owner_only()
    {
        if (OperatingSystem.IsWindows()) return;

        await Linux().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_paths.SaltFilePath));
    }

    [Fact]
    public async Task An_unlock_failure_leaves_its_cause_in_the_log()
    {
        // L'utente vede un solo "non riuscito" per ogni causa: la causa vera deve restare da qualche parte.
        File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        await File.WriteAllBytesAsync(_paths.SaltFilePath, new byte[5]);            // salt di lunghezza sbagliata
        var log = new ListLogger();

        var result = await new DbUnlockService(_paths, platformKeyFactory: null, new NullSecretStore(), log)
            .UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
    }

    [Fact]
    public async Task A_platform_key_that_cannot_read_the_database_leaves_a_warning()
    {
        await SeedAsync(_paths.DatabasePath, new FixedKey(Bytes(7)));
        var log = new ListLogger();

        var result = await new DbUnlockService(_paths, () => new FixedKey(Bytes(99)), new NullSecretStore(), log)
            .UnlockAsync(DbUnlockState.UsePlatformKey, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        AssertOneRefusal(log, DbUnlockState.UsePlatformKey);
    }

    [Fact]
    public async Task The_other_refusals_without_an_exception_leave_a_warning_too()
    {
        // Platform key requested on a system that has none.
        var noKey = new ListLogger();
        await new DbUnlockService(_paths, platformKeyFactory: null, new NullSecretStore(), noKey).UnlockAsync(DbUnlockState.UsePlatformKey, "", rememberOnDevice: false);
        AssertOneRefusal(noKey, DbUnlockState.UsePlatformKey);

        // Creation refused because a database is already there.
        File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        var exists = new ListLogger();
        await new DbUnlockService(_paths, platformKeyFactory: null, new NullSecretStore(), exists).UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, rememberOnDevice: false);
        AssertOneRefusal(exists, DbUnlockState.NeedsCreation);

        // Passphrase entered with the database missing.
        File.Delete(_paths.DatabasePath);
        var missing = new ListLogger();
        await new DbUnlockService(_paths, platformKeyFactory: null, new NullSecretStore(), missing).UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false);
        AssertOneRefusal(missing, DbUnlockState.NeedsPassphrase);
    }

    private static void AssertOneRefusal(ListLogger log, DbUnlockState state)
    {
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(state.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Passphrase, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unexpected_failure_is_logged_and_still_propagates()
    {
        // La libreria nativa che non si carica (il rischio D9 su Linux) non e' fra le eccezioni che il servizio
        // converte in "non riuscito": risale alla finestra, che mostra un messaggio generico. La traccia deve
        // restare nel log comunque, e il comportamento verso il chiamante non cambia.
        var log = new ListLogger();
        var sut = new DbUnlockService(_paths, () => throw new DllNotFoundException("libe_sqlcipher.so"), new NullSecretStore(), log);

        var thrown = await Assert.ThrowsAsync<DllNotFoundException>(
            () => sut.UnlockAsync(DbUnlockState.UsePlatformKey, Passphrase, rememberOnDevice: false));

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(thrown, entry.Exception);
    }

    [Fact]
    public async Task A_cancelled_unlock_is_not_logged_as_a_failure()
    {
        var log = new ListLogger();
        var sut = new DbUnlockService(_paths, () => throw new OperationCanceledException(), new NullSecretStore(), log);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.UnlockAsync(DbUnlockState.UsePlatformKey, Passphrase, rememberOnDevice: false));

        Assert.Empty(log.Entries);
    }

    private sealed class ListLogger : ILogger<DbUnlockService>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception, formatter(state, exception)));
    }

    [Fact]
    public async Task A_corrupt_salt_is_a_failure_not_a_wrong_passphrase()
    {
        // Diciamo all'utente "passphrase errata" solo quando lo è davvero: un salt di lunghezza
        // sbagliata non si risolve riprovando.
        File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        await File.WriteAllBytesAsync(_paths.SaltFilePath, new byte[5]);

        var result = await Linux().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Unsupported_is_a_failure_and_touches_nothing()
    {
        File.WriteAllBytes(_paths.DatabasePath, [1, 2, 3]);
        File.WriteAllBytes(_paths.KeyFilePath, [9]);

        var result = await Linux().UnlockAsync(DbUnlockState.Unsupported, Passphrase, rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
        Assert.False(File.Exists(_paths.SaltFilePath));
    }

    [Fact]
    public async Task The_platform_key_path_needs_no_passphrase()
    {
        var key = new FixedKey(Bytes(7));
        var sut = Windows(key);

        var result = await sut.UnlockAsync(DbUnlockState.UsePlatformKey, "", rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Unlocked, result.Outcome);
        Assert.Same(key, result.Provider);
    }

    [Fact]
    public async Task The_platform_key_that_cannot_read_the_database_is_a_failure_not_a_wrong_passphrase()
    {
        await SeedAsync(_paths.DatabasePath, new FixedKey(Bytes(7)));

        var result = await Windows(new FixedKey(Bytes(99))).UnlockAsync(DbUnlockState.UsePlatformKey, "", rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task UsePlatformKey_without_a_platform_key_is_a_failure()
    {
        var result = await Linux().UnlockAsync(DbUnlockState.UsePlatformKey, "", rememberOnDevice: false);

        Assert.Equal(UnlockOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void The_default_constructor_offers_a_platform_key_only_on_windows()
    {
        var sut = new DbUnlockService(_paths, new NullSecretStore());

        // Su Linux e macOS il costruttore di default NON ha una chiave di piattaforma: primo avvio =
        // NeedsCreation. Su Windows = UsePlatformKey (DPAPI, come WPF).
        Assert.Equal(OperatingSystem.IsWindows() ? DbUnlockState.UsePlatformKey : DbUnlockState.NeedsCreation, sut.DetectState());
    }
}
