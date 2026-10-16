using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Security;

/// <summary>Lo sblocco dalla chiave ricordata nel portachiavi (F2c Task 3), su SQLCipher vero e uno store in memoria.</summary>
[Collection(SqlCipherPoolCollection.Name)]
public sealed class DbUnlockServiceStoredKeyTests : IDisposable
{
    private const string Passphrase = "correct horse battery staple";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-storedkey-" + Guid.NewGuid().ToString("N"));
    private readonly TestPaths _paths;
    private readonly MemoryStore _store = new();

    public DbUnlockServiceStoredKeyTests()
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

    private sealed class MemoryStore : ISecretStore
    {
        public Dictionary<SecretKind, string> Items { get; } = [];
        public int Reads { get; private set; }
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) { Reads++; return Items.GetValueOrDefault(kind); }
        public void Write(SecretKind kind, string value) => Items[kind] = value;
        public void Delete(SecretKind kind) => Items.Remove(kind);
    }

    private sealed class ThrowingStore : ISecretStore
    {
        public bool IsAvailable => false;
        public string? TryRead(SecretKind kind) => throw new SecretStoreUnavailableException("no");
        public void Write(SecretKind kind, string value) => throw new SecretStoreUnavailableException("no");
        public void Delete(SecretKind kind) => throw new SecretStoreUnavailableException("no");
    }

    private DbUnlockService Service(ISecretStore? store = null) => new(_paths, platformKeyFactory: null, store ?? _store);

    private static void Seed(string dbPath, KeyMaterial key)
    {
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
        var v = (long)cmd.ExecuteScalar()!;
        SqliteConnection.ClearAllPools();
        return v;
    }

    /// <summary>Crea un database vero con dentro una riga, ricordando (o no) la chiave.</summary>
    private async Task CreateAsync(bool remember, ISecretStore? store = null)
    {
        var created = await Service(store).UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, remember ? RememberChoice.Remember : RememberChoice.LeaveAsIs);
        created.Outcome.Should().Be(UnlockOutcome.Unlocked);
        using (var key = await created.Provider!.GetKeyAsync()) Seed(_paths.DatabasePath, key);
    }

    [Fact]
    public async Task The_remembered_key_unlocks_without_deriving_from_the_salt()
    {
        await CreateAsync(remember: true);
        File.WriteAllBytes(_paths.SaltFilePath, [1, 2, 3]);   // leggerlo darebbe KeyUnavailable: provato che non si legge

        var result = await Service().TryUnlockWithStoredKeyAsync();

        result.Outcome.Should().Be(StoredKeyOutcome.Unlocked);
        (await ReadAsync(_paths.DatabasePath, result.Provider!)).Should().Be(42);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("malformed")]
    [InlineData("short")]
    [InlineData("long")]
    public async Task A_wrong_or_malformed_remembered_key_is_rejected_and_forgotten(string kind)
    {
        await CreateAsync(remember: false);
        _store.Items[SecretKind.DbKey] = kind switch
        {
            "wrong-key" => Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyMaterial.LengthBytes)),
            "malformed" => "this is !!! not base64",
            "short" => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
            _ => Convert.ToBase64String(RandomNumberGenerator.GetBytes(33)),
        };

        var result = await Service().TryUnlockWithStoredKeyAsync();

        result.Outcome.Should().Be(StoredKeyOutcome.StoredKeyRejected);
        result.Provider.Should().BeNull();
        _store.Items.Should().NotContainKey(SecretKind.DbKey);
    }

    [Fact]
    public async Task No_remembered_key_is_NoStoredKey()
    {
        await CreateAsync(remember: false);

        (await Service().TryUnlockWithStoredKeyAsync()).Outcome.Should().Be(StoredKeyOutcome.NoStoredKey);
    }

    [Fact]
    public async Task A_keyring_that_throws_is_StoreUnavailable()
    {
        await CreateAsync(remember: false);

        (await Service(new ThrowingStore()).TryUnlockWithStoredKeyAsync()).Outcome.Should().Be(StoredKeyOutcome.StoreUnavailable);
    }

    [Fact]
    public async Task Only_a_database_waiting_for_a_passphrase_tries_the_stored_key()
    {
        _store.Items[SecretKind.DbKey] = Convert.ToBase64String(new byte[KeyMaterial.LengthBytes]);   // nessun database: NeedsCreation

        (await Service().TryUnlockWithStoredKeyAsync()).Outcome.Should().Be(StoredKeyOutcome.NoStoredKey);
        _store.Reads.Should().Be(0);
        _store.Items.Should().ContainKey(SecretKind.DbKey, "non e' una chiave respinta: non c'era niente da aprire");
    }

    [Fact]
    public async Task Entering_the_passphrase_with_remember_stores_the_key_and_the_next_start_uses_it()
    {
        await CreateAsync(remember: false);
        _store.Items.Should().BeEmpty();

        var entered = await Service().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, remember: RememberChoice.Remember);

        entered.Outcome.Should().Be(UnlockOutcome.Unlocked);
        _store.Items.Should().ContainKey(SecretKind.DbKey);
        (await Service().TryUnlockWithStoredKeyAsync()).Outcome.Should().Be(StoredKeyOutcome.Unlocked);
    }

    [Fact]
    public async Task Without_remember_nothing_is_written_on_entry_or_on_creation()
    {
        await CreateAsync(remember: false);
        await Service().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, remember: RememberChoice.LeaveAsIs);

        _store.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_wrong_passphrase_never_writes_the_key()
    {
        await CreateAsync(remember: false);

        var r = await Service().UnlockAsync(DbUnlockState.NeedsPassphrase, "incorrect horse battery staple", remember: RememberChoice.Remember);

        r.Outcome.Should().Be(UnlockOutcome.WrongPassphrase);
        _store.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Creation_with_remember_writes_the_key()
    {
        await CreateAsync(remember: true);

        _store.Items.Should().ContainKey(SecretKind.DbKey);
        Convert.FromBase64String(_store.Items[SecretKind.DbKey]).Should().HaveCount(KeyMaterial.LengthBytes);
    }

    [Fact]
    public async Task A_keyring_that_fails_on_write_does_not_change_the_outcome()
    {
        var created = await Service(new ThrowingStore()).UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, remember: RememberChoice.Remember);

        created.Outcome.Should().Be(UnlockOutcome.Unlocked);
    }

    [Fact]
    public async Task The_remembered_key_still_opens_the_database_after_a_restore_from_another_machine()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            File.WriteAllBytes(fixture.Options.SaltFilePath, new byte[16]);   // l'installazione e' a passphrase
            using (var key = await fixture.KeyProvider.GetKeyAsync())
                _store.Items[SecretKind.DbKey] = Convert.ToBase64String(key.RawKey);
            var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CrossMachine", "made-on-linux-v2.18.recbak");

            await fixture.NewRestoreService().RestoreAsync(source, "cross-machine-passphrase",
                new Recordum.Core.Backup.RestoreOptions(SkipSafetySnapshot: true));

            var result = await new DbUnlockService(fixture.Options, platformKeyFactory: null, _store).TryUnlockWithStoredKeyAsync();
            result.Outcome.Should().Be(StoredKeyOutcome.Unlocked);
            result.Provider.Should().NotBeNull();
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static readonly TimeSpan ShortCap = TimeSpan.FromMilliseconds(300);

    /// <summary>Uno store le cui scritture/cancellazioni/letture non tornano finche' non lo si sblocca.</summary>
    private sealed class BlockingStore : ISecretStore, IDisposable
    {
        private readonly ManualResetEventSlim _gate = new();
        public Dictionary<SecretKind, string> Items { get; } = [];
        public Action? OnRead { get; set; }
        public bool BlockWrite { get; set; }
        public bool BlockDelete { get; set; }
        public int Deletes { get; private set; }
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) { OnRead?.Invoke(); return Items.GetValueOrDefault(kind); }
        public void Write(SecretKind kind, string value) { if (BlockWrite) _gate.Wait(); Items[kind] = value; }
        public void Delete(SecretKind kind) { Deletes++; if (BlockDelete) _gate.Wait(); Items.Remove(kind); }
        public void Dispose() { _gate.Set(); _gate.Dispose(); }
    }

    [Fact]
    public async Task A_remember_write_that_never_returns_does_not_hang_the_unlock_on_entry_or_creation()
    {
        using var store = new BlockingStore { BlockWrite = true };
        var sut = new DbUnlockService(_paths, platformKeyFactory: null, store, keyringCap: ShortCap);

        var created = await sut.UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, RememberChoice.Remember).WaitAsync(TimeSpan.FromSeconds(30));
        created.Outcome.Should().Be(UnlockOutcome.Unlocked);
        using (var key = await created.Provider!.GetKeyAsync()) Seed(_paths.DatabasePath, key);

        var entered = await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, RememberChoice.Remember).WaitAsync(TimeSpan.FromSeconds(30));
        entered.Outcome.Should().Be(UnlockOutcome.Unlocked);
    }

    [Fact]
    public async Task A_forget_delete_that_never_returns_does_not_hang_the_unlock()
    {
        await CreateAsync(remember: true);
        using var store = new BlockingStore { BlockDelete = true };
        var sut = new DbUnlockService(_paths, platformKeyFactory: null, store, keyringCap: ShortCap);

        var entered = await sut.UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, RememberChoice.Forget).WaitAsync(TimeSpan.FromSeconds(30));

        entered.Outcome.Should().Be(UnlockOutcome.Unlocked);
    }

    [Fact]
    [Trait("Category", "UnixOnly")]
    public async Task A_disk_error_while_trying_the_stored_key_keeps_the_secret()
    {
        if (OperatingSystem.IsWindows()) return;
        await CreateAsync(remember: true);
        var before = _store.Items[SecretKind.DbKey];
        File.SetUnixFileMode(_paths.DatabasePath, UnixFileMode.None);
        try
        {
            var result = await Service().TryUnlockWithStoredKeyAsync();

            result.Outcome.Should().Be(StoredKeyOutcome.NoStoredKey, "non e' la chiave: e' il disco");
            _store.Items[SecretKind.DbKey].Should().Be(before);
        }
        finally { File.SetUnixFileMode(_paths.DatabasePath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
    }

    [Fact]
    public async Task An_abandoned_attempt_that_finishes_after_cancellation_neither_deletes_nor_touches_the_database()
    {
        await CreateAsync(remember: false);
        using var cts = new CancellationTokenSource();
        using var store = new BlockingStore { OnRead = cts.Cancel };
        store.Items[SecretKind.DbKey] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyMaterial.LengthBytes));   // se arrivasse a CanRead: Rejected + Delete

        var result = await new DbUnlockService(_paths, platformKeyFactory: null, store).TryUnlockWithStoredKeyAsync(cts.Token);

        result.Outcome.Should().Be(StoredKeyOutcome.NoStoredKey);
        store.Deletes.Should().Be(0);
        store.Items.Should().ContainKey(SecretKind.DbKey);
    }

    [Fact]
    public async Task Creation_always_clears_an_old_key_unless_it_writes_the_new_one()
    {
        _store.Items[SecretKind.DbKey] = "chiave-di-un-database-morto";
        var leave = await Service().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, RememberChoice.LeaveAsIs);
        leave.Outcome.Should().Be(UnlockOutcome.Unlocked);
        _store.Items.Should().BeEmpty();
        leave.Provider!.As<IDisposable>().Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_paths.DatabasePath); File.Delete(_paths.SaltFilePath);

        _store.Items[SecretKind.DbKey] = "chiave-di-un-database-morto";
        (await Service().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, RememberChoice.Forget)).Outcome.Should().Be(UnlockOutcome.Unlocked);
        _store.Items.Should().BeEmpty();
        File.Delete(_paths.DatabasePath); File.Delete(_paths.SaltFilePath);

        _store.Items[SecretKind.DbKey] = "chiave-di-un-database-morto";
        (await Service().UnlockAsync(DbUnlockState.NeedsCreation, Passphrase, RememberChoice.Remember)).Outcome.Should().Be(UnlockOutcome.Unlocked);
        Convert.FromBase64String(_store.Items[SecretKind.DbKey]).Should().HaveCount(KeyMaterial.LengthBytes);
    }

    [Fact]
    public async Task On_entry_an_enabled_unchecked_box_deletes_the_key_and_a_disabled_box_leaves_it()
    {
        await CreateAsync(remember: true);
        var key = _store.Items[SecretKind.DbKey];

        (await Service().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, RememberChoice.LeaveAsIs)).Outcome.Should().Be(UnlockOutcome.Unlocked);
        _store.Items[SecretKind.DbKey].Should().Be(key, "casella non attiva: nessuna richiesta dell'utente");

        (await Service().UnlockAsync(DbUnlockState.NeedsPassphrase, Passphrase, RememberChoice.Forget)).Outcome.Should().Be(UnlockOutcome.Unlocked);
        _store.Items.Should().BeEmpty("casella attiva e tolta: l'utente ha chiesto di dimenticare");
    }

    [Fact]
    public async Task A_wrong_passphrase_never_forgets_the_key()
    {
        await CreateAsync(remember: true);

        (await Service().UnlockAsync(DbUnlockState.NeedsPassphrase, "incorrect horse battery staple", RememberChoice.Forget)).Outcome.Should().Be(UnlockOutcome.WrongPassphrase);

        _store.Items.Should().ContainKey(SecretKind.DbKey);
    }
}
