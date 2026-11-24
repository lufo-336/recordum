using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Backup;
using Recordum.Data.Models;
using Recordum.Data.Services;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Recordum.Data.Tests.Fixtures.Sync;

namespace Recordum.Data.Tests.Sync;

public sealed class SyncServiceTests : IDisposable
{
    private const string SharedPassphrase = "correct-horse-battery-staple";
    private static readonly Guid H1 = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid H2 = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid H3 = new("11111111-0000-0000-0000-000000000003");
    private static readonly Guid G1 = new("11111111-0000-0000-0000-0000000000AA");
    private static readonly Guid N1 = new("22222222-0000-0000-0000-000000000001");

    private readonly string _rootDir = Path.Combine(Path.GetTempPath(), $"recordum-sync-svc-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;

    public SyncServiceTests()
    {
        _sharedFolder = Path.Combine(_rootDir, "cloud-folder");
        Directory.CreateDirectory(_sharedFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDir))
                Directory.Delete(_rootDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    private static SyncFileCipher CreateFastCipher() =>
        new((passphrase, salt, _, _, _) =>
        {
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase + ":" + Convert.ToHexString(salt)));
            return Task.FromResult(key);
        });

    private static HabitNode NewHabit(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NodeType = NodeType.Habit,
        HabitType = HabitType.Action,
        Color = "#336699",
    };

    [Fact]
    public async Task RunMergesAllPeersAndWritesOwnFile()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        // C parte vuoto e senza identity pre-seminata: si collega da solo al primo giro e genera il proprio GUID in sync_meta.
        await using var c = await CreateNodeAsync(deviceName: null, _sharedFolder, joined: false, startMs: 3_000);

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Morning Run")));
        var runA1 = await a.Service.RunAsync(CancellationToken.None);
        runA1.Ran.Should().BeTrue();
        runA1.OwnFileWritten.Should().BeTrue();
        runA1.Peers.Should().BeEmpty();

        await b.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "Read Book")));
        var runB1 = await b.Service.RunAsync(CancellationToken.None);
        runB1.Ran.Should().BeTrue();
        runB1.OwnFileWritten.Should().BeTrue();
        runB1.Peers.Should().ContainSingle(p => p.DeviceName == "Fedora" && p.Problem == null);

        var runC1 = await c.Service.RunAsync(CancellationToken.None);
        runC1.Ran.Should().BeTrue();
        runC1.OwnFileWritten.Should().BeTrue();
        runC1.Peers.Select(p => p.DeviceName).Should().BeEquivalentTo(["Fedora", "Laptop"]);
        runC1.Peers.Should().AllSatisfy(p => p.Problem.Should().BeNull());

        // Verifica che C abbia creato la propria riga in sync_meta con un GUID D maiuscolo e MachineName.
        var cMeta = await c.QueryAsync(db => db.SyncMeta.AsNoTracking().SingleAsync());
        Guid.TryParseExact(cMeta.DeviceId, "D", out _).Should().BeTrue();
        cMeta.DeviceId.Should().Be(cMeta.DeviceId.ToUpperInvariant());
        cMeta.DeviceName.Should().Be(Environment.MachineName);
        c.RealDeviceIdentity.DeviceName.Should().Be(Environment.MachineName);

        var runA2 = await a.Service.RunAsync(CancellationToken.None);
        runA2.Ran.Should().BeTrue();
        runA2.OwnFileWritten.Should().BeTrue();

        foreach (var node in (TestNode[])[a, b, c])
        {
            var names = await node.QueryAsync(db => db.HabitNodes.AsNoTracking().OrderBy(n => n.Name).Select(n => n.Name).ToListAsync());
            names.Should().Equal("Morning Run", "Read Book");
            Directory.EnumerateFileSystemEntries(node.Paths.TempDirectory).Should().BeEmpty();
        }

        var statusA = await a.Service.GetStatusAsync(CancellationToken.None);
        statusA.LastRun.Should().Be(a.Clock.UtcNow);
        statusA.Peers.Should().HaveCount(2);
        statusA.OwnFileBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TruncatedPeerFileSkippedOthersApplied()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        await using var c = await CreateNodeAsync("Tablet", _sharedFolder, joined: true, startMs: 3_000);

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "From Fedora")));
        await a.Service.RunAsync(CancellationToken.None);

        await b.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "From Laptop")));
        await b.Service.RunAsync(CancellationToken.None);

        // Tronca il file di B a metà: GCM fallisce con CannotOpen.
        var bFile = await b.GetOwnFilePathAsync();
        var fullBytes = await File.ReadAllBytesAsync(bFile);
        await File.WriteAllBytesAsync(bFile, fullBytes[..(fullBytes.Length / 2)]);

        var result = await c.Service.RunAsync(CancellationToken.None);

        result.Ran.Should().BeTrue();
        result.Peers.Should().ContainSingle(p => p.DeviceName == "Fedora" && p.Problem == null);
        result.Peers.Should().ContainSingle(p => p.Problem == SyncPeerProblem.CannotOpen);

        var cHabits = await c.QueryAsync(db => db.HabitNodes.AsNoTracking().Select(n => n.Name).ToListAsync());
        cHabits.Should().Equal("From Fedora");
    }

    [Fact]
    public async Task BrokenJsonPeerFileAppliesNothing()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);

        await a.EditAsync(1_100, db =>
        {
            db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "valid-tag" });
            db.HabitNodes.Add(NewHabit(H1, "Row 1"));
            db.HabitNodes.Add(NewHabit(H2, "Row 2"));
        });

        // Costruisce un file di A con una riga JSON corrotta in data/habit_node.jsonl.
        var aFile = await a.GetOwnFilePathAsync();
        await a.Service.RunAsync(CancellationToken.None);

        using var plainZip = new MemoryStream();
        await using (var encIn = File.OpenRead(aFile))
            await a.Service.ExportPlainAsync(encIn, plainZip, SharedPassphrase, CancellationToken.None);

        plainZip.Position = 0;
        using (var archive = new ZipArchive(plainZip, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("data/habit_node.jsonl")!;
            entry.Delete();
            var replacement = archive.CreateEntry("data/habit_node.jsonl");
            await using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            await writer.WriteAsync("{\"id\":\"bad-json\n");
        }

        plainZip.Position = 0;
        await using (var encOut = File.Create(aFile))
            await a.Service.ReencryptAsync(plainZip, encOut, SharedPassphrase, CancellationToken.None);

        var result = await b.Service.RunAsync(CancellationToken.None);

        result.Ran.Should().BeTrue();
        result.Peers.Should().ContainSingle().Which.Problem.Should().Be(SyncPeerProblem.BrokenFile);
        (await b.QueryAsync(db => db.Tags.CountAsync())).Should().Be(0, "a broken JSON line rejects the whole peer archive");
        (await b.QueryAsync(db => db.HabitNodes.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task NewerMajorFormatReportedAsNewerFormat()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "From A")));
        await a.Service.RunAsync(CancellationToken.None);

        var aFile = await a.GetOwnFilePathAsync();
        using var plainZip = new MemoryStream();
        await using (var encIn = File.OpenRead(aFile))
            await a.Service.ExportPlainAsync(encIn, plainZip, SharedPassphrase, CancellationToken.None);
        plainZip.Position = 0;
        using (var archive = new ZipArchive(plainZip, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("manifest.json")!;
            string manifest;
            using (var reader = new StreamReader(entry.Open()))
                manifest = await reader.ReadToEndAsync();
            entry.Delete();
            await using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open(), new UTF8Encoding(false));
            await writer.WriteAsync(manifest.Replace("\"format_version\":\"1.0\"", "\"format_version\":\"2.0\"", StringComparison.Ordinal));
        }

        plainZip.Position = 0;
        await using (var encOut = File.Create(aFile))
            await a.Service.ReencryptAsync(plainZip, encOut, SharedPassphrase, CancellationToken.None);

        var result = await b.Service.RunAsync(CancellationToken.None);

        result.Peers.Should().ContainSingle().Which.Problem.Should().Be(SyncPeerProblem.NewerFormat);
        (await b.QueryAsync(db => db.HabitNodes.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task SameIdentityFileSkipped()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        var aMeta = await a.QueryAsync(db => db.SyncMeta.AsNoTracking().SingleAsync());

        // Un altro file nella cartella porta lo stesso device_id di A.
        await using var clone = await CreateNodeAsync("Fedora-Clone", _sharedFolder, joined: true, startMs: 2_000, explicitDeviceId: aMeta.DeviceId);
        await clone.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H1, "From Clone")));

        using var cloneZip = new MemoryStream();
        await using (var cloneDb = await clone.Fixture.Factory.CreateDbContextAsync())
        {
            await SyncArchiveWriter.WriteAsync(
                cloneDb,
                cloneZip,
                new SyncWriteOptions(aMeta.DeviceId, "Fedora-Clone", "3.10.0", IncludePhotos: false, WrittenAtMs: 2_100));
        }

        cloneZip.Position = 0;
        var cloneFilePath = Path.Combine(_sharedFolder, "recordum-Fedora-Clone-duplicate.rcsync");
        await using (var outFile = File.Create(cloneFilePath))
            await a.Service.ReencryptAsync(cloneZip, outFile, SharedPassphrase, CancellationToken.None);

        var result = await a.Service.RunAsync(CancellationToken.None);

        result.Ran.Should().BeTrue();
        result.Peers.Should().ContainSingle(p => p.Problem == SyncPeerProblem.SameIdentity);
        (await a.QueryAsync(db => db.HabitNodes.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task OwnFileUnchangedWhenNothingChanged()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Steady Habit")));

        var firstRun = await a.Service.RunAsync(CancellationToken.None);
        firstRun.OwnFileWritten.Should().BeTrue();

        var ownPath = await a.GetOwnFilePathAsync();
        var fixedTimestamp = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(ownPath, fixedTimestamp);

        a.Clock.Advance(TimeSpan.FromMinutes(10));
        var secondRun = await a.Service.RunAsync(CancellationToken.None);

        secondRun.Ran.Should().BeTrue();
        secondRun.OwnFileWritten.Should().BeFalse();
        File.GetLastWriteTimeUtc(ownPath).Should().Be(fixedTimestamp);
    }

    [Fact]
    public async Task CrashDuringWriteKeepsPreviousOwnFile()
    {
        var failNextWrite = false;
        await using var a = await CreateNodeAsync(
            "Fedora",
            _sharedFolder,
            joined: true,
            startMs: 1_000,
            storeFactory: (folder, ownFileName) => new FaultyStore(new FolderSyncFileStore(folder, ownFileName), () => failNextWrite));

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "First Version")));
        await a.Service.RunAsync(CancellationToken.None);

        var ownPath = await a.GetOwnFilePathAsync();
        var previousBytes = await File.ReadAllBytesAsync(ownPath);

        await a.EditAsync(2_000, db => db.HabitNodes.Add(NewHabit(H2, "Second Version")));
        failNextWrite = true;

        var act = () => a.Service.RunAsync(CancellationToken.None);
        await act.Should().ThrowAsync<IOException>().WithMessage("simulated crash mid-write");

        File.Exists(ownPath + ".tmp").Should().BeFalse();
        (await File.ReadAllBytesAsync(ownPath)).Should().Equal(previousBytes);

        // Il file precedente è ancora valido e leggibile.
        using var plainZip = new MemoryStream();
        await using (var encIn = File.OpenRead(ownPath))
            await a.Service.ExportPlainAsync(encIn, plainZip, SharedPassphrase, CancellationToken.None);
        plainZip.Position = 0;
        using var snapshot = SyncArchiveReader.Open(plainZip);
        snapshot.Rows["habit_node"].Should().ContainSingle(r => r.Id == SyncSeed.Stored(H1));
    }

    [Fact]
    public async Task RestoreRegeneratesDeviceIdAndClearsBase()
    {
        const string backupPass = "backup-passphrase-12345";
        const string oldDeviceId = "11111111-2222-3333-4444-555555555555";

        await using var fx = await EncryptedDbFixture.SeedAsync();
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            db.SyncMeta.Add(new SyncMeta
            {
                Id = Guid.NewGuid(),
                DeviceId = oldDeviceId,
                DeviceName = "OldMachine",
                CreatedAt = fx.Clock.UtcNow.UtcDateTime,
            });
            db.SyncBases.Add(new SyncBase
            {
                PeerDeviceId = "peer-01",
                Entity = "habit_node",
                EntityId = SyncSeed.Stored(H1),
                RowJson = "{\"id\":\"1\"}",
            });
            db.SyncTombstones.Add(new SyncTombstone { Entity = "habit_node", EntityId = SyncSeed.Stored(H2), DeletedAtMs = 900, DeviceId = oldDeviceId });
            await db.SaveChangesAsync();
        }

        var realIdentity = new DeviceIdentity(new SingleFactoryProvider(fx.DbFactory));
        realIdentity.DeviceName.Should().Be("OldMachine");

        var backup = await fx.CreateBackupAsync(backupPass);

        var restoreService = new RestoreService(
            fx.DbFactory,
            fx.KeyProvider,
            fx.Options,
            new AesGcmBackupCipher(),
            new BackupArchiveReader(),
            fx.NewBackupService(),
            fx.PluginManager,
            fx.Clock,
            fx.Gate,
            deviceIdentity: realIdentity);

        await restoreService.RestoreAsync(backup.Path, backupPass, new RestoreOptions(SkipSafetySnapshot: true));
        fx.Gate.Reopen();

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            (await db.SyncMeta.CountAsync()).Should().Be(0);
            (await db.SyncBases.CountAsync()).Should().Be(0);
            (await db.SyncTombstones.CountAsync()).Should().Be(1, "spec 8.4-bis: the restore keeps the tombstones of the backup");
        }

        realIdentity.DeviceName.Should().BeNull("RestoreService invalidated the cached device identity");

        // Alla prima richiesta dopo il ripristino, SyncService genera un nuovo device_id in sync_meta.
        var settings = new AppSettingService(fx.DbFactory, fx.Clock, NullLogger<AppSettingService>.Instance);
        var syncService = new SyncService(
            fx.DbFactory,
            settings,
            new FakeBackupService(),
            fx.Options,
            CreateFastCipher(),
            new SyncRunState(),
            fx.Clock,
            realIdentity,
            appVersion: "test");
        await settings.SetAsync(SyncService.FolderSettingKey, _sharedFolder);
        await settings.SetAsync(SyncService.PassphraseSettingKey, SharedPassphrase);
        await settings.SetAsync(SyncService.JoinedSettingKey, "true");

        (await syncService.RunAsync(CancellationToken.None)).Ran.Should().BeTrue();

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var newMeta = await db.SyncMeta.AsNoTracking().SingleAsync();
            newMeta.DeviceId.Should().NotBe(oldDeviceId);
            Guid.TryParseExact(newMeta.DeviceId, "D", out _).Should().BeTrue();
            newMeta.DeviceId.Should().Be(newMeta.DeviceId.ToUpperInvariant());
        }

        realIdentity.DeviceName.Should().Be(Environment.MachineName);
    }

    [Fact]
    public async Task JoinAlwaysBacksUpFirst()
    {
        await using var peer = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await peer.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Peer Habit")));
        await peer.Service.RunAsync(CancellationToken.None);

        await using var a = await CreateNodeAsync("Laptop", _sharedFolder, joined: false, startMs: 2_000);
        await a.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "Local Habit")));

        var habitCountDuringBackup = -1;
        string? backupPassphrase = null;
        a.Backups.OnCreateAsync = async passphrase =>
        {
            backupPassphrase = passphrase;
            habitCountDuringBackup = await a.QueryAsync(db => db.HabitNodes.CountAsync());
        };

        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NeedsChoice);

        await a.Service.JoinAsync(SyncJoinChoice.StartFromOthers, CancellationToken.None);

        a.Backups.Calls.Should().Be(1);
        habitCountDuringBackup.Should().Be(1, "the safety backup must run BEFORE StartFromOthers wipes local tables");
        backupPassphrase.Should().Be(SharedPassphrase, "the backup is protected by the passphrase the user just typed");
        a.Backups.LastKind.Should().Be(BackupKind.Manual, "'Before restore' would mislead in the backup history");

        await using var b = await CreateNodeAsync("Tablet", _sharedFolder, joined: false, startMs: 3_000);
        await b.EditAsync(3_100, db => db.HabitNodes.Add(NewHabit(H3, "Tablet Habit")));

        await b.Service.JoinAsync(SyncJoinChoice.KeepThisDeviceToo, CancellationToken.None);
        b.Backups.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(SyncJoinChoice.StartFromOthers)]
    [InlineData(SyncJoinChoice.KeepThisDeviceToo)]
    public async Task FailedBackupStopsTheJoin(SyncJoinChoice choice)
    {
        await using var peer = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await peer.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Peer Habit")));
        await peer.Service.RunAsync(CancellationToken.None);

        await using var a = await CreateNodeAsync("Laptop", _sharedFolder, joined: false, startMs: 2_000);
        await a.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "Local Habit")));
        a.Backups.OnCreateAsync = _ => throw new IOException("disk full");

        var act = () => a.Service.JoinAsync(choice, CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        (await a.QueryAsync(db => db.HabitNodes.AsNoTracking().Select(n => n.Name).ToListAsync())).Should().Equal("Local Habit");
        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NeedsChoice);
        File.Exists(await a.GetOwnFilePathAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task StartFromOthersRefusedWhenNoPeerFileOpens()
    {
        await using var peer = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await peer.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Peer Habit")));
        await peer.Service.RunAsync(CancellationToken.None);

        await using var a = await CreateNodeAsync("Laptop", _sharedFolder, joined: false, startMs: 2_000);
        await a.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "Local Habit")));
        await a.Service.SetPassphraseAsync("a-mistyped-passphrase", CancellationToken.None);

        var act = () => a.Service.JoinAsync(SyncJoinChoice.StartFromOthers, CancellationToken.None);

        await act.Should().ThrowAsync<SyncJoinRefusedException>();
        a.Backups.Calls.Should().Be(0);
        (await a.QueryAsync(db => db.HabitNodes.AsNoTracking().Select(n => n.Name).ToListAsync())).Should().Equal("Local Habit");
        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NeedsChoice);
        Directory.EnumerateFileSystemEntries(a.Paths.TempDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task StartFromOthersReplacesLocalDataWithoutTombstones()
    {
        await using var peer = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await peer.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Peer Habit")));
        await peer.Service.RunAsync(CancellationToken.None);

        await using var joining = await CreateNodeAsync("Laptop", _sharedFolder, joined: false, startMs: 2_000);
        await joining.EditAsync(2_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H2, "Local Discarded Habit"));
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H2, BodyMd = "local note" });
        });
        await SyncSeed.SeedAsync(joining.Fixture);   // una riga per ogni entità che viaggia, più una lapide
        await joining.EditAsync(2_200, db =>
        {
            db.HabitNodes.Add(new HabitNode { Id = G1, Name = "Local Group", NodeType = NodeType.Group, Color = "#123456" });
            db.HabitNodes.Add(new HabitNode
            {
                Id = H3, ParentId = G1, Name = "Local Child", NodeType = NodeType.Habit, HabitType = HabitType.Action, Color = "#654321",
            });
        });
        await joining.Settings.SetAsync("theme", "dark");

        (await joining.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NeedsChoice);

        await joining.Service.JoinAsync(SyncJoinChoice.StartFromOthers, CancellationToken.None);

        joining.Backups.Calls.Should().Be(1);
        (await joining.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.Joined);

        var habits = await joining.QueryAsync(db => db.HabitNodes.IgnoreQueryFilters().AsNoTracking().Select(n => n.Name).ToListAsync());
        habits.Should().Equal("Peer Habit");
        (await joining.QueryAsync(db => db.Notes.CountAsync())).Should().Be(0);
        (await joining.QueryAsync(db => db.SyncTombstones.CountAsync())).Should().Be(0, "wiping local data on StartFromOthers must not emit tombstones");
        (await joining.Settings.GetAsync("theme")).Should().Be("dark", "local-only settings remain untouched");
        foreach (var entity in SyncCatalog.Entities.Where(e => e.PublicName != "app_setting"))
        {
            var here = await joining.CountAsync(entity.Table);
            var there = await peer.CountAsync(entity.Table);
            here.Should().Be(there, $"after StartFromOthers '{entity.Table}' holds only the other devices' rows");
        }
        (await peer.Service.RunAsync(CancellationToken.None)).Ran.Should().BeTrue();
        (await peer.QueryAsync(db => db.HabitNodes.IgnoreQueryFilters().Select(n => n.Name).ToListAsync()))
            .Should().Equal(["Peer Habit"], "the discarded rows vanish only here, never on the others");
    }

    [Fact]
    public async Task KeepThisDeviceTooMergesBoth()
    {
        await using var peer = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await peer.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Peer Habit")));
        await peer.Service.RunAsync(CancellationToken.None);

        await using var joining = await CreateNodeAsync("Laptop", _sharedFolder, joined: false, startMs: 2_000);
        await joining.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "Local Kept Habit")));

        (await joining.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NeedsChoice);

        await joining.Service.JoinAsync(SyncJoinChoice.KeepThisDeviceToo, CancellationToken.None);

        joining.Backups.Calls.Should().Be(1);
        (await joining.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.Joined);

        var joiningHabits = await joining.QueryAsync(db => db.HabitNodes.AsNoTracking().OrderBy(n => n.Name).Select(n => n.Name).ToListAsync());
        joiningHabits.Should().Equal("Local Kept Habit", "Peer Habit");

        await peer.Service.RunAsync(CancellationToken.None);
        var peerHabits = await peer.QueryAsync(db => db.HabitNodes.AsNoTracking().OrderBy(n => n.Name).Select(n => n.Name).ToListAsync());
        peerHabits.Should().Equal("Local Kept Habit", "Peer Habit");
    }

    [Fact]
    public async Task DisconnectStopsRunsAndKeepsData()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Kept After Disconnect")));
        await a.Service.RunAsync(CancellationToken.None);

        var ownPath = await a.GetOwnFilePathAsync();
        File.Exists(ownPath).Should().BeTrue();

        await a.Service.DisconnectAsync(CancellationToken.None);

        (await a.Settings.GetAsync(SyncService.FolderSettingKey)).Should().BeNull();
        (await a.Settings.GetAsync(SyncService.JoinedSettingKey)).Should().BeNull();
        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NotConfigured);

        File.Exists(ownPath).Should().BeTrue("disconnecting keeps the last own sync file in place");
        (await a.QueryAsync(db => db.HabitNodes.CountAsync())).Should().Be(1, "disconnecting keeps all local data");

        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        await b.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H3, "New Peer Habit")));
        await b.Service.RunAsync(CancellationToken.None);

        var runAfterDisconnect = await a.Service.RunAsync(CancellationToken.None);
        runAfterDisconnect.Ran.Should().BeFalse();
        (await a.QueryAsync(db => db.HabitNodes.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task PlainExportRoundTrip()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "RoundTrip Habit"));
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "Plain export body" });
        });
        await a.Service.RunAsync(CancellationToken.None);

        var ownPath = await a.GetOwnFilePathAsync();
        using var zip1 = new MemoryStream();
        await using (var rcsync1 = File.OpenRead(ownPath))
            await a.Service.ExportPlainAsync(rcsync1, zip1, SharedPassphrase, CancellationToken.None);

        zip1.Position = 0;
        using var rcsync2 = new MemoryStream();
        await a.Service.ReencryptAsync(zip1, rcsync2, SharedPassphrase, CancellationToken.None);

        rcsync2.Position = 0;
        using var zip2 = new MemoryStream();
        await a.Service.ExportPlainAsync(rcsync2, zip2, SharedPassphrase, CancellationToken.None);

        zip1.Position = 0;
        zip2.Position = 0;
        using var snap1 = SyncArchiveReader.Open(zip1);
        using var snap2 = SyncArchiveReader.Open(zip2);

        snap2.Manifest.DeviceId.Should().Be(snap1.Manifest.DeviceId);
        snap2.Manifest.DeviceName.Should().Be(snap1.Manifest.DeviceName);
        snap2.Rows["habit_node"].Single().Id.Should().Be(SyncSeed.Stored(H1));
        snap2.Rows["note"].Single().Fields["body_md"].GetString().Should().Be("Plain export body");
    }

    [Fact]
    public async Task PassphraseNeverWrittenInFile()
    {
        const string secretPassphrase = "ultra-secret-sync-passphrase-never-in-archive";
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.Service.SetPassphraseAsync(secretPassphrase, CancellationToken.None);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Privacy Check")));

        await a.Service.RunAsync(CancellationToken.None);

        var ownPath = await a.GetOwnFilePathAsync();
        var encryptedText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(ownPath));
        encryptedText.Should().NotContain(secretPassphrase);

        using var plainZip = new MemoryStream();
        await using (var rcsync = File.OpenRead(ownPath))
            await a.Service.ExportPlainAsync(rcsync, plainZip, secretPassphrase, CancellationToken.None);

        plainZip.Position = 0;
        using var archive = new ZipArchive(plainZip, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            content.Should().NotContain(secretPassphrase, $"entry '{entry.FullName}' must never contain the sync passphrase");
            content.Should().NotContain(SyncService.PassphraseSettingKey, $"entry '{entry.FullName}' must never contain the sync.passphrase setting key");
        }
    }

    [Fact]
    public async Task OwnFileRewrittenWhenAnEditIsOlderThanTheNewestRow()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Old Name")));
        // Una riga con l'ora più avanti di tutte (come quelle di un pari con l'orologio avanti).
        await a.EditAsync(9_000, db => db.HabitNodes.Add(NewHabit(H2, "From The Future")));
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();

        // Modifica di una riga esistente con un'ora minore del massimo: stesso numero di righe, stesso massimo.
        await a.EditAsync(3_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H1)).Name = "New Name");
        var run = await a.Service.RunAsync(CancellationToken.None);

        run.OwnFileWritten.Should().BeTrue("the edit must reach the other devices");
        using var zip = new MemoryStream();
        await using (var file = File.OpenRead(await a.GetOwnFilePathAsync()))
            await a.Service.ExportPlainAsync(file, zip, SharedPassphrase, CancellationToken.None);
        zip.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zip);
        snapshot.Rows["habit_node"].Single(r => r.Id == SyncSeed.Stored(H1)).Fields["name"].GetString().Should().Be("New Name");
    }

    [Fact]
    public async Task DatabaseCopiedToAnotherMachineGetsNewIdentity()
    {
        const string copiedId = "AAAAAAAA-1111-2222-3333-444444444444";
        await using var a = await CreateNodeAsync(
            "OldMachine", _sharedFolder, joined: true, startMs: 1_000, explicitDeviceId: copiedId, machineName: "NewMachine");
        await a.EditAsync(1_100, db => db.SyncBases.Add(new SyncBase
        {
            PeerDeviceId = "peer-01", Entity = "habit_node", EntityId = SyncSeed.Stored(H1), RowJson = "{}",
        }));
        a.RealDeviceIdentity.DeviceName.Should().Be("OldMachine");

        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();

        var meta = await a.QueryAsync(db => db.SyncMeta.AsNoTracking().SingleAsync());
        meta.DeviceId.Should().NotBe(copiedId);
        meta.DeviceName.Should().Be("NewMachine");
        (await a.QueryAsync(db => db.SyncBases.CountAsync())).Should().Be(0, "the base belonged to the other machine");
        a.RealDeviceIdentity.DeviceName.Should().Be("NewMachine");
        File.Exists(Path.Combine(_sharedFolder, SyncService.BuildOwnFileName("NewMachine", meta.DeviceId))).Should().BeTrue();
        File.Exists(Path.Combine(_sharedFolder, SyncService.BuildOwnFileName("OldMachine", copiedId))).Should().BeFalse();
    }

    [Fact]
    public async Task CopiedDatabaseOnSameNamedMachineDetectedAsSameIdentity()
    {
        const string sharedId = "BBBBBBBB-1111-2222-3333-444444444444";
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000, explicitDeviceId: sharedId);
        await using var clone = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 2_000, explicitDeviceId: sharedId);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "From A")));
        await a.Service.RunAsync(CancellationToken.None);
        var ownPath = await a.GetOwnFilePathAsync();
        (await clone.GetOwnFilePathAsync()).Should().Be(ownPath, "same identity, same machine name: the same file");

        // Il clone, senza impronta, adotta il file (non si distingue da una perdita delle impostazioni) e lo riscrive.
        await clone.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "From Clone")));
        (await clone.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();
        var cloneBytes = await File.ReadAllBytesAsync(ownPath);

        // A trova il suo file scritto da un altro: non lo sovrascrive e lo stato lo dice.
        await a.EditAsync(3_000, db => db.HabitNodes.Add(NewHabit(H3, "Later On A")));
        var aRun = await a.Service.RunAsync(CancellationToken.None);
        aRun.OwnFileWritten.Should().BeFalse();
        aRun.Peers.Should().ContainSingle(p => p.Problem == SyncPeerProblem.SameIdentity);
        (await a.Service.GetStatusAsync(CancellationToken.None)).Peers.Should().Contain(p => p.Problem == SyncPeerProblem.SameIdentity);
        (await File.ReadAllBytesAsync(ownPath)).Should().Equal(cloneBytes);

        // La via d'uscita: scollegato e ricollegato, A rientra come un dispositivo nuovo con un file nuovo.
        await a.Service.DisconnectAsync(CancellationToken.None);
        await a.Settings.SetAsync(SyncService.FolderSettingKey, _sharedFolder);
        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NeedsChoice);
        await a.Service.JoinAsync(SyncJoinChoice.KeepThisDeviceToo, CancellationToken.None);

        var meta = await a.QueryAsync(db => db.SyncMeta.AsNoTracking().SingleAsync());
        meta.DeviceId.Should().NotBe(sharedId);
        var newPath = await a.GetOwnFilePathAsync();
        newPath.Should().NotBe(ownPath);
        File.Exists(newPath).Should().BeTrue();
        a.RunState.Peers.Should().NotContain(p => p.Problem == SyncPeerProblem.SameIdentity);
        (await a.QueryAsync(db => db.HabitNodes.Select(n => n.Name).OrderBy(n => n).ToListAsync()))
            .Should().Equal("From A", "From Clone", "Later On A");
    }

    [Fact]
    public async Task CrashBetweenRenameAndPromoteRecovers()
    {
        var crashAfterRename = false;
        await using var a = await CreateNodeAsync(
            "Fedora",
            _sharedFolder,
            joined: true,
            startMs: 1_000,
            storeFactory: (folder, own) => new CrashAfterWriteStore(new FolderSyncFileStore(folder, own), () => crashAfterRename));
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "First")));
        await a.Service.RunAsync(CancellationToken.None);

        await a.EditAsync(2_000, db => db.HabitNodes.Add(NewHabit(H2, "Second")));
        crashAfterRename = true;
        await FluentActions.Awaiting(() => a.Service.RunAsync(CancellationToken.None)).Should().ThrowAsync<IOException>();
        crashAfterRename = false;

        var run = await a.Service.RunAsync(CancellationToken.None);

        run.Peers.Should().NotContain(p => p.Problem == SyncPeerProblem.SameIdentity, "the file was written here");
        run.OwnFileWritten.Should().BeTrue("the crashed round never recorded its signature");
        (await a.Settings.GetAsync(SyncService.LastWrittenHashSettingKey)).Should().Be(HashOf(await a.GetOwnFilePathAsync()));
        (await a.Settings.GetAsync(SyncService.PendingWrittenHashSettingKey)).Should().BeEmpty();
    }

    [Fact]
    public async Task AbsentHashAdoptsExistingOwnFile()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "First")));
        await a.Service.RunAsync(CancellationToken.None);
        // Impostazioni perse, sync_meta rimasta: nessuna impronta né in sospeso.
        await a.QueryAsync(db => db.AppSettings
            .Where(s => s.Key == SyncService.LastWrittenHashSettingKey || s.Key == SyncService.PendingWrittenHashSettingKey)
            .ExecuteDeleteAsync());
        var ownPath = await a.GetOwnFilePathAsync();

        var adopt = await a.Service.RunAsync(CancellationToken.None);
        adopt.Peers.Should().NotContain(p => p.Problem == SyncPeerProblem.SameIdentity);
        (await a.Settings.GetAsync(SyncService.LastWrittenHashSettingKey)).Should().Be(HashOf(ownPath));

        await a.EditAsync(2_000, db => db.HabitNodes.Add(NewHabit(H2, "Second")));
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();
    }

    private static string HashOf(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class CrashAfterWriteStore(ISyncFileStore inner, Func<bool> crash) : ISyncFileStore
    {
        public Task<IReadOnlyList<SyncPeerFile>> ListPeerFilesAsync(CancellationToken ct) => inner.ListPeerFilesAsync(ct);

        public Task<Stream> OpenReadAsync(SyncPeerFile file, CancellationToken ct) => inner.OpenReadAsync(file, ct);

        public async Task WriteOwnAsync(Func<Stream, Task> write, CancellationToken ct)
        {
            await inner.WriteOwnAsync(write, ct);   // rinominato: il file nuovo è al suo posto
            if (crash()) throw new IOException("simulated crash before the hash is promoted");
        }
    }

    [Fact]
    public async Task NewPassphraseRewritesOwnFile()
    {
        const string newPassphrase = "a-brand-new-sync-passphrase";
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Habit")));
        await a.Service.RunAsync(CancellationToken.None);

        await a.Service.SetPassphraseAsync(newPassphrase, CancellationToken.None);
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue("nothing changed but the passphrase");

        var ownPath = await a.GetOwnFilePathAsync();
        await using (var file = File.OpenRead(ownPath))
            await a.Service.ExportPlainAsync(file, new MemoryStream(), newPassphrase, CancellationToken.None);
        var old = async () =>
        {
            await using var file = File.OpenRead(ownPath);
            await a.Service.ExportPlainAsync(file, new MemoryStream(), SharedPassphrase, CancellationToken.None);
        };
        (await old.Should().ThrowAsync<SyncFileException>()).Which.Error.Should().Be(SyncFileError.CannotOpen);
    }

    [Fact]
    public async Task PhotosTravelUnlessTurnedOff()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Habit")));

        await a.Service.RunAsync(CancellationToken.None);
        (await ReadOwnManifestAsync(a)).IncludesPhotos.Should().BeTrue("photos are on until the user turns them off (spec 9)");

        await a.Settings.SetAsync(SyncService.IncludePhotosSettingKey, "false");
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();
        (await ReadOwnManifestAsync(a)).IncludesPhotos.Should().BeFalse();
    }

    /// <summary>Ruling 17: il proprio file dice, per ogni pari, il written_at_ms dell'ultimo suo file unito qui. Viaggia
    /// con la prossima scrittura (un merged_from cambiato da solo non riscrive il file: due dispositivi si riscriverebbero
    /// a vicenda a ogni giro), e "Scollega" lo dimentica.</summary>
    [Fact]
    public async Task OwnFileSaysWhichPeerFilesWereMerged()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        await b.EditAsync(2_100, db => db.HabitNodes.Add(NewHabit(H2, "Read Book")));
        await b.Service.RunAsync(CancellationToken.None);
        var bManifest = await ReadOwnManifestAsync(b);

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Morning Run")));
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();
        (await ReadOwnManifestAsync(a)).MergedFrom.Should().Equal(
            new Dictionary<string, long> { [bManifest.DeviceId] = bManifest.WrittenAtMs });

        (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue("b took a's habit");
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten
            .Should().BeFalse("only merged_from changed: it waits for the next real change");

        await a.Service.DisconnectAsync(CancellationToken.None);
        (await a.Settings.GetAsync(SyncService.MergedFromSettingKey)).Should().BeNull();
    }

    [Fact]
    public async Task JoinStateIsReadOnly()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: false, startMs: 1_000);

        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.Joined, "an empty database joins by itself");
        (await a.Settings.GetAsync(SyncService.JoinedSettingKey)).Should().BeNull("only a round writes it");

        await a.Service.RunAsync(CancellationToken.None);
        (await a.Settings.GetAsync(SyncService.JoinedSettingKey)).Should().Be("true");
    }

    private static async Task<SyncManifest> ReadOwnManifestAsync(TestNode node)
    {
        using var zip = new MemoryStream();
        await using (var file = File.OpenRead(await node.GetOwnFilePathAsync()))
            await node.Service.ExportPlainAsync(file, zip, SharedPassphrase, CancellationToken.None);
        zip.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zip);
        return snapshot.Manifest;
    }

    [Fact]
    public async Task RoundsNeverOverlap()
    {
        var inside = 0;
        var maxInside = 0;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var a = await CreateNodeAsync(
            "Fedora",
            _sharedFolder,
            joined: true,
            startMs: 1_000,
            storeFactory: (folder, own) => new GatedStore(new FolderSyncFileStore(folder, own), async () =>
            {
                var now = Interlocked.Increment(ref inside);
                InterlockedMax(ref maxInside, now);
                firstEntered.TrySetResult();
                await release.Task;
                Interlocked.Decrement(ref inside);
            }));

        // Il giro del pianificatore (thread del pool) e "Unisci ora" o un collegamento dalla UI, sullo stesso processo.
        var scheduled = Task.Run(() => a.Service.RunAsync(CancellationToken.None));
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var manual = Task.Run(() => a.Service.RunAsync(CancellationToken.None));
        var disconnect = Task.Run(() => a.Service.DisconnectAsync(CancellationToken.None));
        await Task.Delay(300);
        Volatile.Read(ref inside).Should().Be(1, "the second round waits for the first");
        manual.IsCompleted.Should().BeFalse();
        disconnect.IsCompleted.Should().BeFalse("disconnecting waits for the round in progress");

        release.SetResult();
        await Task.WhenAll(scheduled, manual, disconnect).WaitAsync(TimeSpan.FromSeconds(30));
        maxInside.Should().Be(1);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }

    private sealed class GatedStore(ISyncFileStore inner, Func<Task> onList) : ISyncFileStore
    {
        public async Task<IReadOnlyList<SyncPeerFile>> ListPeerFilesAsync(CancellationToken ct)
        {
            await onList();
            return await inner.ListPeerFilesAsync(ct);
        }

        public Task<Stream> OpenReadAsync(SyncPeerFile file, CancellationToken ct) => inner.OpenReadAsync(file, ct);

        public Task WriteOwnAsync(Func<Stream, Task> write, CancellationToken ct) => inner.WriteOwnAsync(write, ct);
    }

    [Fact]
    public async Task StartFromOthersKeepsLocalDataWhenNoPeerMerges()
    {
        await using var peer = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await peer.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Peer Habit")));
        await peer.Service.RunAsync(CancellationToken.None);

        // Il file del pari supera SyncArchiveReader.Open (e quindi AnyPeerOpensAsync) ma fallisce in SyncRowCodec.ToDb.
        var peerFile = await peer.GetOwnFilePathAsync();
        using (var plainZip = new MemoryStream())
        {
            await using (var encIn = File.OpenRead(peerFile))
                await peer.Service.ExportPlainAsync(encIn, plainZip, SharedPassphrase, CancellationToken.None);

            plainZip.Position = 0;
            using (var archive = new ZipArchive(plainZip, ZipArchiveMode.Update, leaveOpen: true))
            {
                var entry = archive.GetEntry("data/habit_node.jsonl")!;
                string text;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    text = await reader.ReadToEndAsync();
                entry.Delete();
                await using var writer = new StreamWriter(archive.CreateEntry("data/habit_node.jsonl").Open(), new UTF8Encoding(false));
                await writer.WriteAsync(text.Replace("\"node_type\":\"Habit\"", "\"node_type\":\"NotAType\"", StringComparison.Ordinal));
            }

            plainZip.Position = 0;
            await using var encOut = File.Create(peerFile);
            await peer.Service.ReencryptAsync(plainZip, encOut, SharedPassphrase, CancellationToken.None);
        }

        await using var joining = await CreateNodeAsync("Laptop", _sharedFolder, joined: false, startMs: 2_000);
        await joining.EditAsync(2_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H2, "Local Kept Habit"));
            db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = Guid.NewGuid(), Code = "local-unit", Name = "Local Unit", Symbol = "lu" });
            db.ScaleTemplates.Add(new ScaleTemplate { Id = Guid.NewGuid(), Code = "local-scale", Name = "Local Scale", MinValue = 1, MaxValue = 5 });
        });
        var unitsBefore = await joining.CountAsync("units_of_measure");
        var scalesBefore = await joining.CountAsync("scale_templates");

        var act = () => joining.Service.JoinAsync(SyncJoinChoice.StartFromOthers, CancellationToken.None);

        (await act.Should().ThrowAsync<SyncJoinRefusedException>())
            .Which.Reason.Should().Be(SyncJoinRefusal.NoReadablePeer);
        (await joining.QueryAsync(db => db.HabitNodes.AsNoTracking().Select(n => n.Name).ToListAsync()))
            .Should().Equal("Local Kept Habit");
        (await joining.CountAsync("units_of_measure")).Should().Be(unitsBefore);
        (await joining.CountAsync("scale_templates")).Should().Be(scalesBefore);
        (await joining.Settings.GetAsync(SyncService.JoinedSettingKey)).Should().NotBe("true");
        File.Exists(await joining.GetOwnFilePathAsync()).Should().BeFalse();
        Directory.EnumerateFileSystemEntries(joining.Paths.TempDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task JoinStateIsNotConfiguredWithoutPassphrase()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: false, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Local Habit")));
        await a.QueryAsync(db => db.AppSettings.Where(s => s.Key == SyncService.PassphraseSettingKey).ExecuteDeleteAsync());

        (await a.Service.GetJoinStateAsync(CancellationToken.None)).Should().Be(SyncJoinState.NotConfigured);

        var act = () => a.Service.JoinAsync(SyncJoinChoice.KeepThisDeviceToo, CancellationToken.None);
        (await act.Should().ThrowAsync<SyncJoinRefusedException>())
            .Which.Reason.Should().Be(SyncJoinRefusal.MissingConfiguration);
    }

    [Fact]
    public async Task FieldTimeOnlyChangeRewritesOwnFile()
    {
        // 1. Solo sync_field_ms cambia (stessi valori, stessa updated_at_ms), con scrittura fallita al primo giro.
        var failA = false;
        await using var a = await CreateNodeAsync(
            "Fedora", _sharedFolder, joined: true, startMs: 1_000,
            storeFactory: (folder, own) => new FaultyStore(new FolderSyncFileStore(folder, own), () => failA));
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 1_000);

        await a.EditAsync(1_000, db =>
        {
            var h = NewHabit(H1, "Init");
            h.Color = "#111111";
            db.HabitNodes.Add(h);
        });
        await a.EditAsync(2_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H1)).Name = "Walk");
        await a.Service.RunAsync(CancellationToken.None);

        await b.EditAsync(1_500, db =>
        {
            var h = NewHabit(H1, "Init");
            h.Color = "#111111";
            db.HabitNodes.Add(h);
        });
        await b.EditAsync(2_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H1)).Name = "Walk");
        await b.Service.RunAsync(CancellationToken.None);

        failA = true;
        await FluentActions.Awaiting(() => a.Service.RunAsync(CancellationToken.None)).Should().ThrowAsync<IOException>();
        failA = false;
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue("sync_field_ms changed");

        // 2. Solo il contenuto della foto cambia (stessa updated_at_ms e sync_field_ms). La firma guarda le lunghezze, non
        // i byte: un contenuto diverso della stessa lunghezza aspetta la prossima scrittura vera.
        var rawPhoto = SyncSeed.CreateSamplePhotoBytes();
        byte[] rawAlt = [.. rawPhoto, 0];
        var (photo1, photo2) = StringComparer.Ordinal.Compare(
            Convert.ToHexStringLower(SHA256.HashData(rawPhoto)),
            Convert.ToHexStringLower(SHA256.HashData(rawAlt))) < 0
            ? (rawAlt, rawPhoto)
            : (rawPhoto, rawAlt);
        var thumb = SyncSeed.CreateSampleThumbnailBytes();
        var attId = new Guid("33333333-0000-0000-0000-000000000001");
        await a.EditAsync(3_000, db =>
        {
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "p" });
            db.Attachments.Add(new Recordum.Data.Models.Attachment
            {
                Id = attId, NoteId = N1, Filename = "a.jpg", MimeType = "image/jpeg",
                FileSizeBytes = photo1.Length, Content = photo1, Thumbnail = thumb,
            });
        });
        await a.EditAsync(4_000, async db => (await db.Attachments.SingleAsync()).Filename = "same.jpg");
        await a.Service.RunAsync(CancellationToken.None);

        await b.EditAsync(3_000, db =>
        {
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "p" });
            db.Attachments.Add(new Recordum.Data.Models.Attachment
            {
                Id = attId, NoteId = N1, Filename = "a.jpg", MimeType = "image/jpeg",
                FileSizeBytes = photo2.Length, Content = photo2, Thumbnail = thumb,
            });
        });
        await b.EditAsync(4_000, async db => (await db.Attachments.SingleAsync()).Filename = "same.jpg");
        await b.Service.RunAsync(CancellationToken.None);

        failA = true;
        await FluentActions.Awaiting(() => a.Service.RunAsync(CancellationToken.None)).Should().ThrowAsync<IOException>();
        failA = false;
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue("photo content changed without timestamp change");

        // 3. Solo alias_of di una lapide già esistente cambia (stesso deleted_at_ms).
        var tagMin = new Guid("10000000-0000-0000-0000-000000000001");
        var tagMax = new Guid("90000000-0000-0000-0000-000000000009");
        await a.EditAsync(5_000, db =>
        {
            db.Tags.Add(new Tag { Id = tagMin, Name = "outdoor" });
            db.Tags.Add(new Tag { Id = tagMax, Name = "temp" });
        });
        await a.EditAsync(7_000, db => db.Tags.Where(t => t.Id == tagMax).ExecuteDeleteAsync());
        await a.Service.RunAsync(CancellationToken.None);

        await b.EditAsync(5_000, db => db.Tags.Add(new Tag { Id = tagMin, Name = "outdoor" }));
        await b.EditAsync(6_000, db => db.SyncTombstones.Add(new SyncTombstone
        {
            Entity = "tag",
            EntityId = SyncSeed.Stored(tagMax),
            DeletedAtMs = 6_000,
            AliasOf = SyncSeed.Stored(tagMin),
        }));
        await b.Service.RunAsync(CancellationToken.None);

        failA = true;
        await FluentActions.Awaiting(() => a.Service.RunAsync(CancellationToken.None)).Should().ThrowAsync<IOException>();
        failA = false;
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue("tombstone alias_of changed");
    }

    [Fact]
    public async Task DisconnectClearsRunState()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Kept")));
        await a.Service.RunAsync(CancellationToken.None);
        await a.Settings.SetAsync(SyncService.PendingWrittenHashSettingKey, "pending-hash");

        (await a.Service.GetStatusAsync(CancellationToken.None)).LastRun.Should().NotBeNull();

        await a.Service.DisconnectAsync(CancellationToken.None);

        (await a.Settings.GetAsync(SyncService.LastRunMsSettingKey)).Should().BeNull();
        (await a.Settings.GetAsync(SyncService.LastWrittenSignatureSettingKey)).Should().BeNull();
        (await a.Settings.GetAsync(SyncService.LastWrittenHashSettingKey)).Should().BeNull();
        (await a.Settings.GetAsync(SyncService.PendingWrittenHashSettingKey)).Should().BeNull();
        var status = await a.Service.GetStatusAsync(CancellationToken.None);
        status.LastRun.Should().BeNull();
        status.OwnFileBytes.Should().Be(0);
        status.Peers.Should().BeEmpty();
    }

    [Fact]
    public async Task Echo_happens_once_then_stops()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Changed")));

        bool[] written =
        [
            (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
        ];

        written.Should().Equal(true, true, false, false, false, false);
    }

    [Fact]
    public async Task Echo_with_three_devices_stops_after_one_round()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        await using var c = await CreateNodeAsync("Tablet", _sharedFolder, joined: true, startMs: 3_000);

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "Changed")));
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();

        bool[] written =
        [
            (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await c.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await c.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
            (await c.Service.RunAsync(CancellationToken.None)).OwnFileWritten,
        ];

        written.Should().Equal(true, true, false, false, false, false, false, false);
    }

    [Fact]
    public async Task Ack_is_saved_only_after_the_own_file_is_written()
    {
        var folderA = Path.Combine(_rootDir, "cloud-a");
        var folderB = Path.Combine(_rootDir, "cloud-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        var failB = false;
        await using var a = await CreateNodeAsync("Fedora", folderA, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync(
            "Laptop",
            folderB,
            joined: true,
            startMs: 2_000,
            storeFactory: (folder, own) => new FaultyStore(new FolderSyncFileStore(folder, own), () => failB));

        // B ha la versione più recente, quindi unire il file di A è un no-op: solo l'eco fa riscrivere B.
        await a.EditAsync(1_000, db => db.HabitNodes.Add(NewHabit(H1, "Older")));
        await a.Service.RunAsync(CancellationToken.None);

        await b.EditAsync(2_000, db => db.HabitNodes.Add(NewHabit(H1, "Newer")));
        (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();

        var aFile = await a.GetOwnFilePathAsync();
        File.Copy(aFile, Path.Combine(folderB, Path.GetFileName(aFile)), overwrite: true);

        failB = true;
        await FluentActions.Awaiting(() => b.Service.RunAsync(CancellationToken.None)).Should().ThrowAsync<IOException>();
        (await b.Settings.GetAsync(SyncService.AckedDataSettingKey)).Should().BeNullOrEmpty();

        failB = false;
        var recoveryRun = await b.Service.RunAsync(CancellationToken.None);
        recoveryRun.OwnFileWritten.Should().BeTrue("the ack was not saved on the failed write, so B still emits the echo");
        (await b.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeFalse();
    }

    [Fact]
    public async Task Manifest_carries_the_written_signature()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "First")));
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();

        var sig1 = await a.Settings.GetAsync(SyncService.LastWrittenSignatureSettingKey);
        sig1.Should().NotBeNullOrEmpty();
        (await ReadOwnManifestAsync(a)).DataSignature.Should().Be(sig1);

        await a.EditAsync(2_000, db => db.HabitNodes.Add(NewHabit(H2, "Second")));
        (await a.Service.RunAsync(CancellationToken.None)).OwnFileWritten.Should().BeTrue();

        var sig2 = await a.Settings.GetAsync(SyncService.LastWrittenSignatureSettingKey);
        sig2.Should().NotBeNullOrEmpty().And.NotBe(sig1);
        (await ReadOwnManifestAsync(a)).DataSignature.Should().Be(sig2);
    }

    [Fact]
    public async Task DisconnectAndStartFromOthersClearAckedData()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);

        await a.EditAsync(1_100, db => db.HabitNodes.Add(NewHabit(H1, "From A")));
        await a.Service.RunAsync(CancellationToken.None);
        await b.Service.RunAsync(CancellationToken.None);

        (await b.Settings.GetAsync(SyncService.AckedDataSettingKey)).Should().NotBeNullOrEmpty();
        await b.Service.DisconnectAsync(CancellationToken.None);
        (await b.Settings.GetAsync(SyncService.AckedDataSettingKey)).Should().BeNull();

        await using var joining = await CreateNodeAsync("Tablet", _sharedFolder, joined: false, startMs: 3_000);
        await joining.EditAsync(3_100, db => db.HabitNodes.Add(NewHabit(H2, "Local Discarded")));
        await joining.Settings.SetAsync(SyncService.AckedDataSettingKey, "{\"old-peer\":\"old-sig\"}");

        await joining.Service.JoinAsync(SyncJoinChoice.StartFromOthers, CancellationToken.None);

        var ackedAfterJoin = await joining.Settings.GetAsync(SyncService.AckedDataSettingKey);
        ackedAfterJoin.Should().NotBeNullOrEmpty().And.NotContain("old-peer");
    }

    [Fact]
    public async Task Identity_creation_stamps_existing_photos_with_this_device()
    {
        await using var a = await CreateNodeAsync(
            deviceName: null, _sharedFolder, joined: true, startMs: 1_000, machineName: "Scrivania");
        var att1 = Guid.NewGuid();
        var att2 = Guid.NewGuid();
        var att3 = Guid.NewGuid();
        var photo = SyncSeed.CreateSamplePhotoBytes();
        var thumb = SyncSeed.CreateSampleThumbnailBytes();
        await a.EditAsync(1_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "Diario"));
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "foto" });
            db.Attachments.AddRange(
                new Recordum.Data.Models.Attachment
                {
                    Id = att1, NoteId = N1, Filename = "a.jpg", MimeType = "image/jpeg",
                    FileSizeBytes = photo.Length, Content = photo, Thumbnail = thumb, OriginDeviceName = null,
                },
                new Recordum.Data.Models.Attachment
                {
                    Id = att2, NoteId = N1, Filename = "b.jpg", MimeType = "image/jpeg",
                    FileSizeBytes = photo.Length, Content = photo, Thumbnail = thumb, OriginDeviceName = null,
                },
                new Recordum.Data.Models.Attachment
                {
                    Id = att3, NoteId = N1, Filename = "c.jpg", MimeType = "image/jpeg",
                    FileSizeBytes = photo.Length, Content = photo, Thumbnail = thumb, OriginDeviceName = "Telefono",
                });
        });

        await a.Service.RunAsync(CancellationToken.None);

        var all = await a.QueryAsync(db => db.Attachments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.OriginDeviceName));
        all[att1].Should().Be("Scrivania");
        all[att2].Should().Be("Scrivania");
        all[att3].Should().Be("Telefono");
    }

    [Fact]
    public async Task DatabaseCopiedToAnotherMachineDoesNotRestampPhotos()
    {
        const string copiedId = "CCCCCCCC-1111-2222-3333-444444444444";
        await using var a = await CreateNodeAsync(
            "OldMachine", _sharedFolder, joined: true, startMs: 1_000, explicitDeviceId: copiedId, machineName: "NewMachine");
        var attId = Guid.NewGuid();
        var thumb = SyncSeed.CreateSampleThumbnailBytes();
        await a.EditAsync(1_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "Diario"));
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "foto" });
            db.Attachments.Add(new Recordum.Data.Models.Attachment
            {
                Id = attId, NoteId = N1, Filename = "old.jpg", MimeType = "image/jpeg",
                FileSizeBytes = 100, Content = null, Thumbnail = thumb, OriginDeviceName = null,
            });
        });

        await a.Service.RunAsync(CancellationToken.None);

        var att = await a.QueryAsync(db => db.Attachments.AsNoTracking().SingleAsync(x => x.Id == attId));
        att.OriginDeviceName.Should().BeNull("regenerating identity (Ruling 18-19) must not touch existing photos");
    }

    [Fact]
    public async Task Origin_travels_with_the_photo()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Telefono", _sharedFolder, joined: true, startMs: 2_000);
        await b.Settings.SetAsync(SyncService.IncludePhotosSettingKey, "false");

        await a.EditAsync(1_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "Diario"));
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "foto" });
        });
        var attSvcA = new AttachmentService(a.Fixture.Factory, a.Clock, a.Fixture.PluginContext, a.RealDeviceIdentity);
        var imported = new ImportedPhoto(
            Guid.NewGuid(), "tramonto.jpg", "image/jpeg", SyncSeed.CreateSamplePhotoBytes(), SyncSeed.CreateSampleThumbnailBytes());
        await attSvcA.AddAsync(N1, imported, CancellationToken.None);

        await a.Service.RunAsync(CancellationToken.None);
        await b.Service.RunAsync(CancellationToken.None);

        var attSvcB = new AttachmentService(b.Fixture.Factory, b.Clock, b.Fixture.PluginContext, b.RealDeviceIdentity);
        var onB = (await attSvcB.ListAllAsync(CancellationToken.None)).Should().ContainSingle().Subject;
        onB.HasContent.Should().BeFalse();
        onB.OriginDeviceName.Should().Be("Fedora");
    }

    [Fact]
    public async Task Two_blocks_in_one_note_are_one_item()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        var day = new DateOnly(2026, 11, 20);
        await a.EditAsync(1_100, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "Corsa"));
            db.Notes.Add(new Note
            {
                Id = N1,
                HabitNodeId = H1,
                Category = NoteCategory.Veloce,
                TargetDate = day,
                BodyMd = "testa\n\n— Telefono · 2026-11-20 10:00 —\nprima copia molto lunga\n\n— Tablet · 2026-11-20 11:00 —\nseconda copia",
            });
        });

        var items = await a.Service.ListItemsToFixAsync(CancellationToken.None);

        var item = items.Should().ContainSingle().Subject;
        item.Kind.Should().Be(SyncItemKind.Note);
        item.EntityId.Should().Be(N1);
        item.HabitNodeId.Should().Be(H1);
        item.HabitName.Should().Be("Corsa");
        item.HabitColor.Should().Be("#336699");
        item.Day.Should().Be(day);
        item.Excerpt.Should().StartWith("— Telefono · 2026-11-20 10:00 — prima copia");
        item.Excerpt.Length.Should().BeLessThanOrEqualTo(80);
    }

    [Fact]
    public async Task A_diary_entry_with_a_block_is_an_item_with_its_day()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await a.Settings.SetAsync("daily_reset_time", "04:00");
        // 02:30 UTC con reset alle 04:00 appartiene al giorno prima (2026-11-19).
        var beforeResetMs = new DateTimeOffset(2026, 11, 20, 2, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var longBody = new string('x', 90);
        await a.EditAsync(beforeResetMs, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "Diario"));
            db.Notes.Add(new Note
            {
                Id = N1,
                HabitNodeId = H1,
                Category = NoteCategory.Diario,
                TargetDate = null,
                BodyMd = $"oggi\n\n— Telefono · 2026-11-19 22:15 —\n{longBody}",
            });
        });

        var items = await a.Service.ListItemsToFixAsync(CancellationToken.None);

        var item = items.Should().ContainSingle().Subject;
        item.Kind.Should().Be(SyncItemKind.DiaryEntry);
        item.Day.Should().Be(new DateOnly(2026, 11, 19));
        item.Excerpt.Length.Should().BeLessThanOrEqualTo(80);
        item.Excerpt.Should().StartWith("— Telefono · 2026-11-19 22:15 —");
    }

    [Fact]
    public async Task A_record_note_with_a_block_is_an_item()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        var compId = new Guid("44444444-0000-0000-0000-000000000001");
        var completedMs = new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await a.EditAsync(completedMs, db =>
        {
            db.HabitNodes.Add(NewHabit(H1, "Acqua"));
            db.Completions.Add(new Completion
            {
                Id = compId,
                HabitNodeId = H1,
                CompletedAt = new DateTime(2026, 11, 21, 18, 0, 0, DateTimeKind.Utc),
                CompletedAtMs = completedMs,
                Notes = "vincitore\n\n— Laptop · 2026-11-21 17:45 —\nnota persa",
            });
        });

        var items = await a.Service.ListItemsToFixAsync(CancellationToken.None);

        var item = items.Should().ContainSingle().Subject;
        item.Kind.Should().Be(SyncItemKind.RecordNote);
        item.EntityId.Should().Be(compId);
        item.HabitNodeId.Should().Be(H1);
        item.Day.Should().Be(new DateOnly(2026, 11, 21));
        item.Excerpt.Should().Be("— Laptop · 2026-11-21 17:45 — nota persa");
    }

    [Fact]
    public async Task A_zero_duration_period_is_an_item()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        var periodId = new Guid("55555555-0000-0000-0000-000000000001");
        var startedMs = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await a.EditAsync(startedMs, db =>
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = H1,
                Name = "Fumo",
                NodeType = NodeType.Habit,
                HabitType = HabitType.Resistance,
                Color = "#CC4422",
            });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = periodId,
                HabitNodeId = H1,
                StartedAtMs = startedMs,
                EndedAtMs = startedMs,
            });
        });

        var items = await a.Service.ListItemsToFixAsync(CancellationToken.None);

        var item = items.Should().ContainSingle().Subject;
        item.Kind.Should().Be(SyncItemKind.RepairedPeriod);
        item.EntityId.Should().Be(periodId);
        item.HabitNodeId.Should().Be(H1);
        item.HabitName.Should().Be("Fumo");
        item.HabitColor.Should().Be("#CC4422");
        item.Day.Should().Be(new DateOnly(2026, 11, 18));
        item.Excerpt.Should().BeEmpty();
    }

    [Fact]
    public async Task An_accepted_period_is_not_an_item()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        await using var b = await CreateNodeAsync("Laptop", _sharedFolder, joined: true, startMs: 2_000);
        var periodId = new Guid("55555555-0000-0000-0000-000000000002");
        var startedMs = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await a.EditAsync(startedMs, db =>
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = H1,
                Name = "Fumo",
                NodeType = NodeType.Habit,
                HabitType = HabitType.Resistance,
                Color = "#CC4422",
            });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = periodId,
                HabitNodeId = H1,
                StartedAtMs = startedMs,
                EndedAtMs = startedMs,
            });
        });

        await a.Service.AcceptRepairAsync(periodId, CancellationToken.None);

        (await a.Service.ListItemsToFixAsync(CancellationToken.None)).Should().BeEmpty();
        // L'accettazione viaggia verso gli altri dispositivi via sync.accepted_repairs.
        await a.Service.RunAsync(CancellationToken.None);
        await b.Service.RunAsync(CancellationToken.None);
        (await b.Service.ListItemsToFixAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Accepting_twice_keeps_one_id()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        var periodId = new Guid("55555555-0000-0000-0000-000000000003");

        await a.Service.AcceptRepairAsync(periodId, CancellationToken.None);
        await a.Service.AcceptRepairAsync(periodId, CancellationToken.None);

        var raw = await a.Settings.GetAsync(SyncService.AcceptedRepairsSettingKey);
        var ids = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(raw!);
        ids.Should().Equal(periodId);
    }

    [Fact]
    public async Task Status_counts_items_not_blocks()
    {
        await using var a = await CreateNodeAsync("Fedora", _sharedFolder, joined: true, startMs: 1_000);
        var periodId = new Guid("55555555-0000-0000-0000-000000000004");
        var startedMs = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await a.EditAsync(startedMs, db =>
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = H1,
                Name = "Fumo",
                NodeType = NodeType.Habit,
                HabitType = HabitType.Resistance,
                Color = "#CC4422",
            });
            db.Notes.Add(new Note
            {
                Id = N1,
                HabitNodeId = H1,
                Category = NoteCategory.Veloce,
                TargetDate = new DateOnly(2026, 11, 19),
                BodyMd = "testa\n\n— A · 2026-11-19 10:00 —\nuno\n\n— B · 2026-11-19 11:00 —\ndue",
            });
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = periodId,
                HabitNodeId = H1,
                StartedAtMs = startedMs,
                EndedAtMs = startedMs,
            });
        });

        var status = await a.Service.GetStatusAsync(CancellationToken.None);
        status.ItemsToFix.Should().Be(2);
    }

    private async Task<TestNode> CreateNodeAsync(
        string? deviceName,
        string sharedFolder,
        bool joined,
        long startMs,
        string? explicitDeviceId = null,
        Func<string, string, ISyncFileStore>? storeFactory = null,
        string? machineName = null)
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(startMs));
        var fixture = new DbFixture(clock);

        if (deviceName is not null)
        {
            fixture.DeviceIdentity.DeviceName = deviceName;
            await using var db = await fixture.Factory.CreateDbContextAsync();
            db.SyncMeta.Add(new SyncMeta
            {
                Id = Guid.NewGuid(),
                DeviceId = explicitDeviceId ?? Guid.NewGuid().ToString("D").ToUpperInvariant(),
                DeviceName = deviceName,
                CreatedAt = clock.UtcNow.UtcDateTime,
            });
            await db.SaveChangesAsync();
        }

        var nodeDir = Path.Combine(_rootDir, $"node-{Guid.NewGuid():N}");
        var tempDir = Path.Combine(nodeDir, "tmp");
        var backupsDir = Path.Combine(nodeDir, "backups");
        var attachDir = Path.Combine(nodeDir, "attachments");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(backupsDir);
        Directory.CreateDirectory(attachDir);

        var paths = new EncryptedDbFixture.TestBackupPaths(
            DatabasePath: Path.Combine(nodeDir, "recordum.db"),
            KeyFilePath: Path.Combine(nodeDir, "db.key"),
            SaltFilePath: Path.Combine(nodeDir, "db.salt"),
            AttachmentsDirectory: attachDir,
            BackupsDirectory: backupsDir,
            AutoBackupKeyPath: Path.Combine(nodeDir, "autobackup.key"),
            TempDirectory: tempDir);

        var settings = new AppSettingService(fixture.Factory, clock, NullLogger<AppSettingService>.Instance);
        await settings.SetAsync(SyncService.FolderSettingKey, sharedFolder);
        await settings.SetAsync(SyncService.PassphraseSettingKey, SharedPassphrase);
        if (joined)
            await settings.SetAsync(SyncService.JoinedSettingKey, "true");

        var realDeviceIdentity = new DeviceIdentity(new SingleFactoryProvider(fixture.Factory));

        var backups = new FakeBackupService();
        var runState = new SyncRunState();
        var service = new SyncService(
            fixture.Factory,
            settings,
            backups,
            paths,
            CreateFastCipher(),
            runState,
            clock,
            realDeviceIdentity,
            appVersion: "test",
            storeFactory: storeFactory,
            machineName: machineName ?? deviceName);

        return new TestNode(fixture, clock, settings, backups, paths, realDeviceIdentity, service, sharedFolder, runState);
    }

    private sealed class TestNode(
        DbFixture fixture,
        FakeClock clock,
        IAppSettingService settings,
        FakeBackupService backups,
        EncryptedDbFixture.TestBackupPaths paths,
        DeviceIdentity realDeviceIdentity,
        SyncService service,
        string sharedFolder,
        SyncRunState runState) : IAsyncDisposable
    {
        public SyncRunState RunState { get; } = runState;
        public DbFixture Fixture { get; } = fixture;
        public FakeClock Clock { get; } = clock;
        public IAppSettingService Settings { get; } = settings;
        public FakeBackupService Backups { get; } = backups;
        public EncryptedDbFixture.TestBackupPaths Paths { get; } = paths;
        public DeviceIdentity RealDeviceIdentity { get; } = realDeviceIdentity;
        public SyncService Service { get; } = service;

        public Task EditAsync(long atMs, Action<RecordumDbContext> edit) =>
            EditAsync(atMs, db =>
            {
                edit(db);
                return Task.CompletedTask;
            });

        public async Task EditAsync(long atMs, Func<RecordumDbContext, Task> edit)
        {
            Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(atMs));
            await using var db = await Fixture.Factory.CreateDbContextAsync();
            await edit(db);
            await db.SaveChangesAsync();
        }

        public async Task<T> QueryAsync<T>(Func<RecordumDbContext, Task<T>> query)
        {
            await using var db = await Fixture.Factory.CreateDbContextAsync();
            return await query(db);
        }

        public Task<long> CountAsync(string table) => QueryAsync(async db =>
        {
            await db.Database.OpenConnectionAsync();
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100 // nomi di tabella dal catalogo
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
#pragma warning restore CA2100
            return (long)(await cmd.ExecuteScalarAsync())!;
        });

        public async Task<string> GetOwnFilePathAsync()
        {
            var meta = await QueryAsync(db => db.SyncMeta.AsNoTracking().SingleAsync());
            return Path.Combine(sharedFolder, SyncService.BuildOwnFileName(meta.DeviceName!, meta.DeviceId));
        }

        public ValueTask DisposeAsync() => Fixture.DisposeAsync();
    }

    private sealed class FakeBackupService : IBackupService
    {
        public int Calls { get; private set; }
        public BackupKind? LastKind { get; private set; }
        public Func<string, Task>? OnCreateAsync { get; set; }

        public async Task<BackupResult> CreateAsync(string destinationPath, string passphrase, BackupOptions options, CancellationToken ct = default)
        {
            Calls++;
            LastKind = options.Kind;
            if (OnCreateAsync is not null)
                await OnCreateAsync(passphrase);

            var manifest = new BackupManifest(
                SchemaVersion: "2.21",
                AppVersion: "3.10.0",
                CreatedAtUtc: DateTime.UnixEpoch,
                Kind: options.Kind,
                SizeBytesUncompressed: 0,
                Counts: new BackupCounts(0, 0, 0, 0),
                Plugins: [],
                Permissions: new PermissionState(new Dictionary<string, IReadOnlyList<string>>()));
            return new BackupResult(Guid.NewGuid(), destinationPath, 0, manifest);
        }
    }

    private sealed class SingleFactoryProvider(IDbContextFactory<RecordumDbContext> factory) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IDbContextFactory<RecordumDbContext>) ? factory : null;
    }

    private sealed class FaultyStore(ISyncFileStore inner, Func<bool> shouldFail) : ISyncFileStore
    {
        public Task<IReadOnlyList<SyncPeerFile>> ListPeerFilesAsync(CancellationToken ct) =>
            inner.ListPeerFilesAsync(ct);

        public Task<Stream> OpenReadAsync(SyncPeerFile file, CancellationToken ct) =>
            inner.OpenReadAsync(file, ct);

        public Task WriteOwnAsync(Func<Stream, Task> write, CancellationToken ct) =>
            inner.WriteOwnAsync(async stream =>
            {
                if (shouldFail())
                {
                    await stream.WriteAsync(new byte[16], ct);
                    throw new IOException("simulated crash mid-write");
                }

                await write(stream);
            }, ct);
    }
}
