using System.Security.Cryptography;
using System.Text;
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
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Recordum.Data.Sync;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;

namespace Recordum.Data.Tests.Fixtures;

/// <summary>
/// Creates a real SQLCipher-encrypted database in a temp directory, suitable for backup/restore integration tests.
/// The key is a fixed random one, not DPAPI: backup and restore must run on Linux too (they are the bridge between
/// the two dogfooding machines). DPAPI itself is covered by <c>DpapiKeyProviderTests</c>.
/// </summary>
public sealed class EncryptedDbFixture : IAsyncDisposable
{
    public string RootDir { get; }
    public TestBackupPaths Options { get; }
    public IDbContextFactory<RecordumDbContext> DbFactory { get; }
    /// <summary>Lo stesso cancello che la factory ha fra gli interceptor e che il restore chiude (F2c-2).</summary>
    public DatabaseGate Gate { get; private init; } = null!;
    public IDbKeyProvider KeyProvider { get; }
    public IBackupLogService LogService { get; }
    public FakeClock Clock { get; }
    public FakeDeviceIdentity DeviceIdentity { get; }
    public IPluginManager PluginManager { get; }

    private EncryptedDbFixture(
        string rootDir,
        TestBackupPaths options,
        IDbContextFactory<RecordumDbContext> dbFactory,
        IDbKeyProvider keyProvider,
        IBackupLogService logService,
        FakeClock clock,
        FakeDeviceIdentity deviceIdentity,
        IPluginManager pluginManager)
    {
        RootDir = rootDir;
        Options = options;
        DbFactory = dbFactory;
        KeyProvider = keyProvider;
        LogService = logService;
        Clock = clock;
        DeviceIdentity = deviceIdentity;
        PluginManager = pluginManager;
    }

    /// <param name="keyProvider">Null = a fixed random key. The fixture generator passes DPAPI on Windows.</param>
    public static async Task<EncryptedDbFixture> SeedAsync(IDbKeyProvider? keyProvider = null)
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"recordum-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        var dbPath = Path.Combine(rootDir, "recordum.db");
        var keyPath = Path.Combine(rootDir, "db.key");
        var saltPath = Path.Combine(rootDir, "db.salt");
        var attachmentsDir = Path.Combine(rootDir, "attachments");
        var backupsDir = Path.Combine(rootDir, "backups");
        var autoBackupKeyPath = Path.Combine(rootDir, "autobackup.key");
        var tempDir = Path.Combine(rootDir, "tmp");
        Directory.CreateDirectory(attachmentsDir);
        Directory.CreateDirectory(backupsDir);
        Directory.CreateDirectory(tempDir);

        var options = new TestBackupPaths(dbPath, keyPath, saltPath, attachmentsDir, backupsDir, autoBackupKeyPath, tempDir);
        keyProvider ??= new FixedKeyProvider(RandomNumberGenerator.GetBytes(KeyMaterial.LengthBytes));
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        // Initialize encrypted DB: open with key, run migrations
        using var key = await keyProvider.GetKeyAsync();
        using (var initConn = SqlCipherConnectionFactory.Open(dbPath, key))
        {
            var initOpts = new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite(initConn)
                .UseSnakeCaseNamingConvention()
                .Options;
            using var initCtx = new RecordumDbContext(initOpts);
            await initCtx.Database.MigrateAsync();
        }
        SqliteConnection.ClearAllPools();

        var deviceIdentity = new FakeDeviceIdentity();
        var gate = new DatabaseGate();
        var dbFactory = new EncryptedDbContextFactory(dbPath, keyProvider,
            new TimestampInterceptor(clock, new FakePluginContext(), deviceIdentity),
            new HabitConfigHistoryInterceptor(clock, NullLogger<HabitConfigHistoryInterceptor>.Instance), gate,
            new SyncConnectionInterceptor(clock));
        await using (var db = await dbFactory.CreateDbContextAsync())
            await SyncTriggerInstaller.EnsureAsync(db, CancellationToken.None);

        var logService = new EncryptedBackupLogService(dbFactory);
        var pluginManager = new StubPluginManager();

        return new EncryptedDbFixture(rootDir, options, dbFactory, keyProvider, logService, clock, deviceIdentity, pluginManager) { Gate = gate };
    }

    // ── Backup/Restore helpers ──────────────────────────────────────────────

    public async Task<BackupResult> CreateBackupAsync(string passphrase)
    {
        var svc = NewBackupService();
        var dest = Path.Combine(Options.BackupsDirectory, $"backup-{Guid.NewGuid():N}.recbak");
        return await svc.CreateAsync(dest, passphrase, new BackupOptions(BackupKind.Manual));
    }

    /// <summary>Creates a .recbak with the manifest schema_version patched for version mismatch tests.</summary>
    public async Task<string> CreateBackupWithSchemaAsync(string passphrase, string overrideSchemaVersion)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"recordum-schema-bk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var normalResult = await CreateBackupAsync(passphrase);
            var cipher = new AesGcmBackupCipher();
            var tempZip = Path.Combine(tempDir, "inner.zip");
            var patchedZip = Path.Combine(tempDir, "patched.zip");

            await using (var src = File.OpenRead(normalResult.Path))
            await using (var dst = File.Create(tempZip))
                await cipher.DecryptAsync(src, dst, passphrase);

            PatchManifestSchemaVersion(tempZip, patchedZip, overrideSchemaVersion);

            var destPath = Path.Combine(Options.BackupsDirectory, $"schema-override-{Guid.NewGuid():N}.recbak");
            await using (var src = File.OpenRead(patchedZip))
            await using (var dst = File.Create(destPath))
                await cipher.EncryptAsync(src, dst, passphrase);

            return destPath;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    public BackupService NewBackupService() => new(
        DbFactory, KeyProvider, Options,
        new AesGcmBackupCipher(), new BackupArchiveWriter(),
        LogService, PluginManager, Clock, appVersion: "3.10.0");

    /// <summary>Il restore riuscito lascia il cancello chiuso fino all'uscita (F2c-2); i test che poi leggono il database
    /// lo riaprono qui. <paramref name="keepGateClosed"/> per chi deve vedere il cancello chiuso.</summary>
    public IRestoreService NewRestoreService(TimeSpan? quiesceTimeout = null, bool keepGateClosed = false)
    {
        var inner = new RestoreService(
            DbFactory, KeyProvider, Options,
            new AesGcmBackupCipher(), new BackupArchiveReader(),
            NewBackupService(), PluginManager, Clock, Gate, quiesceTimeout);
        return keepGateClosed ? inner : new ReopeningRestoreService(inner, Gate);
    }

    private sealed class ReopeningRestoreService(IRestoreService inner, DatabaseGate gate) : IRestoreService
    {
        public Task<RestorePreview> PreviewAsync(string sourcePath, string passphrase, CancellationToken ct = default) =>
            inner.PreviewAsync(sourcePath, passphrase, ct);

        public async Task<RestoreResult> RestoreAsync(string sourcePath, string passphrase, RestoreOptions options, CancellationToken ct = default)
        {
            var result = await inner.RestoreAsync(sourcePath, passphrase, options, ct);
            gate.Reopen();
            return result;
        }
    }

    public BackupScheduler NewScheduler(IAutoBackupKeyStore keys, ILogger<BackupScheduler>? logger = null) =>
        new(DbFactory, NewBackupService(), Options, keys, logger ?? NullLogger<BackupScheduler>.Instance, Clock);

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        await Task.Delay(50);   // let pooled connections close
        try { Directory.Delete(RootDir, recursive: true); } catch { }
    }

    // ── Inner types ─────────────────────────────────────────────────────────

    /// <summary>Returns a COPY every time: <see cref="KeyMaterial.Dispose"/> zeroes the bytes, and callers dispose.</summary>
    internal sealed class FixedKeyProvider(byte[] key) : IDbKeyProvider
    {
        public string ProviderId => "fixed";

        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new KeyMaterial((byte[])key.Clone()));
    }

    public sealed record TestBackupPaths(
        string DatabasePath,
        string KeyFilePath,
        string SaltFilePath,
        string AttachmentsDirectory,
        string BackupsDirectory,
        string AutoBackupKeyPath,
        string TempDirectory) : IBackupStoragePaths;

    private sealed class EncryptedBackupLogService(IDbContextFactory<RecordumDbContext> dbFactory) : IBackupLogService
    {
        public async Task RecordAsync(BackupLog row, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.BackupLogs.Add(row);
            await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<BackupLog>> ListAsync(int take = 50, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            return await db.BackupLogs.AsNoTracking()
                .OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        }

        public async Task<BackupLog?> GetAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            return await db.BackupLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await db.BackupLogs.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        }
    }

    private sealed class EncryptedDbContextFactory(
        string dbPath,
        IDbKeyProvider keyProvider,
        TimestampInterceptor timestamp,
        HabitConfigHistoryInterceptor history,
        DatabaseGate gate,
        SyncConnectionInterceptor sync) : IDbContextFactory<RecordumDbContext>
    {
        // Use connection-string based access so EF Core owns the connection pool.
        // The key is applied via SqlCipherKeyConnectionInterceptor whenever the connection opens.
        private RecordumDbContext BuildContext()
        {
            var opts = new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(history, timestamp, gate, new SqlCipherKeyConnectionInterceptor(keyProvider), sync)
                .Options;
            return new RecordumDbContext(opts);
        }

        public RecordumDbContext CreateDbContext() => BuildContext();

        public Task<RecordumDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(BuildContext());
    }

    private sealed class StubPluginManager : IPluginManager
    {
        private readonly List<PluginManifest> _manifests = [];

        public Task EnableAsync(PluginManifest manifest, CancellationToken ct = default)
        {
            _manifests.RemoveAll(m => m.Id == manifest.Id);
            _manifests.Add(manifest);
            return Task.CompletedTask;
        }

        public Task DisableAsync(string pluginId, CancellationToken ct = default)
        {
            _manifests.RemoveAll(m => m.Id == pluginId);
            return Task.CompletedTask;
        }

        public IReadOnlyCollection<string> ListEnabled() => _manifests.Select(m => m.Id).ToArray();
        public IReadOnlyList<PluginManifest> ListActivatedManifests() => _manifests.AsReadOnly();
    }

    private static void PatchManifestSchemaVersion(string srcZip, string dstZip, string newSchemaVersion)
    {
        using var srcArchive = System.IO.Compression.ZipFile.OpenRead(srcZip);
        using var dstFs = File.Create(dstZip);
        using var dstArchive = new System.IO.Compression.ZipArchive(dstFs, System.IO.Compression.ZipArchiveMode.Create);

        foreach (var entry in srcArchive.Entries)
        {
            var dstEntry = dstArchive.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.Optimal);
            using var srcStream = entry.Open();
            using var dstStream = dstEntry.Open();

            if (entry.FullName == "manifest.json")
            {
                using var sr = new StreamReader(srcStream);
                var json = sr.ReadToEnd();
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, @"""schema_version""\s*:\s*""[^""]*""",
                    $@"""schema_version"": ""{newSchemaVersion}""");
                var bytes = Encoding.UTF8.GetBytes(json);
                dstStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                srcStream.CopyTo(dstStream);
            }
        }
    }
}
