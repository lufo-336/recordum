using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Recordum.Data.Tests.Fixtures.Sync;
using Xunit.Abstractions;
using static Recordum.Data.Tests.Sync.SyncMergerTests;

namespace Recordum.Data.Tests.Sync;

/// <summary>Le prove della re-review di T7 (2026-11-23): giri senza Settle, ponti a tre, file senza merged_from. Il caso del
/// ponte con un ritorno al valore di prima sta in <see cref="FieldMsTests"/>.</summary>
public sealed class SyncThreeDeviceTests(ITestOutputHelper output)
{
    private static readonly Guid H = new("44444444-0000-0000-0000-000000000001");
    private static readonly Guid N = new("44444444-0000-0000-0000-000000000002");

    private static Note QuickNote(Guid id, Guid habit, string body) => new()
    {
        Id = id, HabitNodeId = habit, Category = NoteCategory.Veloce, TargetDate = new DateOnly(2026, 11, 22), BodyMd = body,
    };

    private static Task<Note?> NoteAsync(SyncDevice d, Guid id) =>
        d.QueryAsync(db => db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == id));

    private static readonly FieldInfo MergedFromField =
        typeof(SyncDevice).GetField("_mergedFrom", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static Task<byte[]> WriteOldAsync(SyncDevice d)
    {
        ((Dictionary<string, long>)MergedFromField.GetValue(d)!).Clear();
        return d.WriteFileAsync();
    }

    private static async Task MeshAsync(params SyncDevice[] ds)
    {
        var files = new List<(SyncDevice, byte[])>();
        foreach (var d in ds) files.Add((d, await d.WriteFileAsync()));
        foreach (var d in ds)
            foreach (var (src, f) in files)
                if (src != d) await d.MergeAsync(f);
    }

    private async Task AssertConvergedAsync(params SyncDevice[] ds)
    {
        var dump = await ds[0].CanonicalDumpAsync();
        foreach (var d in ds.Skip(1)) (await d.CanonicalDumpAsync()).Should().Be(dump, $"{d.Name} vs {ds[0].Name}");
        await MeshAsync(ds);
        await MeshAsync(ds);
        (await ds[0].CanonicalDumpAsync()).Should().Be(dump, "fixed point");
    }

    // 1. Round-1 flow without Settle (1 ms gaps): with per-field timestamps the revert is not lost; kept as a convergence guard.
    [Theory]
    [InlineData("a-first")]
    [InlineData("b-first")]
    [InlineData("crossed")]
    public async Task NoSettleRevertConverges(string order)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db => db.HabitNodes.Add(Habit(H, "Run")));
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");
        await b.MergeAsync(await a.WriteFileAsync());
        await b.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run");
        for (var i = 0; i < 4; i++) await TextConflictTests.RoundAsync(a, b, order);
        output.WriteLine($"{order}: a={(await HabitAsync(a, H))!.Name} b={(await HabitAsync(b, H))!.Name}");
        await AssertConvergedAsync(a, b);
    }

    // 2. Cleanup without Settle: blocks may come back, must converge and keep L.
    [Theory]
    [InlineData(false, "a-first")]
    [InlineData(false, "b-first")]
    [InlineData(false, "crossed")]
    [InlineData(true, "a-first")]
    [InlineData(true, "b-first")]
    [InlineData(true, "crossed")]
    public async Task NoSettleCleanupConverges(bool crossed, string order)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db => { db.HabitNodes.Add(Habit(H, "Run")); db.Notes.Add(QuickNote(N, H, "base")); });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());
        await a.EditAsync(200, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "L");
        await b.EditAsync(300, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "W");
        if (crossed) await TextConflictTests.CrossAsync(a, b);
        else { await b.MergeAsync(await a.WriteFileAsync()); await a.MergeAsync(await b.WriteFileAsync()); }
        await b.EditAsync(400, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "W");
        for (var i = 0; i < 4; i++) await TextConflictTests.RoundAsync(a, b, order);
        output.WriteLine($"{crossed}/{order}: a={(await NoteAsync(a, N))!.BodyMd.Replace("\n", "|")}");
        await AssertConvergedAsync(a, b);
    }

    // 4. No-op merge of the SAME peer's older file between write and merge, plus a third device: with per-field timestamps the revert wins; kept as a convergence guard.
    [Fact]
    public async Task NoOpMergeOfPeersOlderFileAndThirdDevice()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var q = new SyncDevice("q");
        await a.EditAsync(10_000, db => db.HabitNodes.Add(Habit(H, "Run")));
        var f0 = await a.WriteFileAsync();
        await b.MergeAsync(f0);
        await q.MergeAsync(f0);
        await a.MergeAsync(await b.WriteFileAsync());
        await a.MergeAsync(await q.WriteFileAsync());
        var fbOld = await b.WriteFileAsync();

        await a.EditAsync(20_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");
        SyncConvergenceTests.Settle(a);
        var f1 = await a.WriteFileAsync();
        await a.MergeAsync(fbOld);                   // no-op, keeps local
        await a.MergeAsync(await q.WriteFileAsync()); // no-op
        await b.MergeAsync(f1);
        await b.EditAsync(40_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run");
        SyncConvergenceTests.Settle(b);
        await a.MergeAsync(await q.WriteFileAsync()); // no-op again before b's file

        await TextConflictTests.CrossAsync(a, b);
        await TextConflictTests.CrossAsync(a, b);
        foreach (var d in new[] { a, b }) (await HabitAsync(d, H))!.Name.Should().Be("Run", d.Name);
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
        await q.MergeAsync(await a.WriteFileAsync());
        await AssertConvergedAsync(a, b, q);
        (await HabitAsync(q, H))!.Name.Should().Be("Run");
    }

    // 5. Walk produced by a merge from Q on A; then Q's newer file (other row changed) no-op for H; B reverts.
    [Fact]
    public async Task MergeProducedVersionSurvivesNoOpFromSamePeerNewerFile()
    {
        var h2 = new Guid("44444444-0000-0000-0000-0000000000a2");
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var q = new SyncDevice("q");
        await a.EditAsync(10_000, db => { db.HabitNodes.Add(Habit(H, "Run")); db.HabitNodes.Add(Habit(h2, "X")); });
        var f0 = await a.WriteFileAsync();
        await b.MergeAsync(f0);
        await q.MergeAsync(f0);
        await a.MergeAsync(await b.WriteFileAsync());
        await a.MergeAsync(await q.WriteFileAsync());

        await q.EditAsync(20_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");
        SyncConvergenceTests.Settle(q);
        await a.MergeAsync(await q.WriteFileAsync()); // produces Walk on A
        var fa = await a.WriteFileAsync();
        await q.EditAsync(30_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == h2)).Name = "Y");
        SyncConvergenceTests.Settle(q);
        await a.MergeAsync(await q.WriteFileAsync()); // H no-op, h2 changes
        await b.MergeAsync(fa);
        await b.EditAsync(60_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run");
        SyncConvergenceTests.Settle(b);

        await TextConflictTests.CrossAsync(a, b);
        await TextConflictTests.CrossAsync(a, b);
        foreach (var d in new[] { a, b }) (await HabitAsync(d, H))!.Name.Should().Be("Run", d.Name);
        await MeshAsync(a, b, q);
        await AssertConvergedAsync(a, b, q);
    }

    // 6. Rescue: deleter C writes files without merged_from; A must rescue (safe), all converge, no text lost.
    [Fact]
    public async Task DeleterWithoutMergedFromConverges()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");
        await a.EditAsync(10_000, db => { db.HabitNodes.Add(Habit(H, "Journal")); db.Notes.Add(QuickNote(N, H, "hello")); });
        SyncConvergenceTests.Settle(a);
        var f1 = await a.WriteFileAsync();
        await b.MergeAsync(f1);
        await a.MergeAsync(await b.WriteFileAsync()); // no-op
        await c.MergeAsync(f1);
        await c.EditAsync(40_000, db => db.Notes.Where(n => n.Id == N).ExecuteDeleteAsync());
        var fc = await WriteOldAsync(c);
        var rep = await a.MergeAsync(fc);
        await b.MergeAsync(fc);
        output.WriteLine($"a rescued {rep.Rescued}");
        for (var i = 0; i < 2; i++)
        {
            var fa = await a.WriteFileAsync(); var fb = await b.WriteFileAsync(); var fc2 = await WriteOldAsync(c);
            await a.MergeAsync(fb); await a.MergeAsync(fc2);
            await b.MergeAsync(fa); await b.MergeAsync(fc2);
            await c.MergeAsync(fa); await c.MergeAsync(fb);
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
        (await a.CanonicalDumpAsync()).Should().Be(await c.CanonicalDumpAsync());
        (await NoteAsync(a, N)).Should().BeNull();
    }

    // 7. Neon ALTA 1: pulizia di un blocco fatta da B prima di unire un file no-op di A (tramite SyncService).
    [Fact]
    public async Task CleanupAfterNoOpMergeConverges()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-sync-neon1-{Guid.NewGuid():N}");
        var folderA = Path.Combine(root, "cloud-a");
        var folderB = Path.Combine(root, "cloud-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        try
        {
            await using var a = await CreateServiceNodeAsync("a", folderA, root, startMs: 1_000);
            await using var b = await CreateServiceNodeAsync("b", folderB, root, startMs: 1_000);

            await a.EditAsync(10_000, db =>
            {
                db.HabitNodes.Add(Habit(H, "Run"));
                db.Notes.Add(QuickNote(N, H, "base"));
            });
            a.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(16_000));
            await a.Service.RunAsync(CancellationToken.None);
            CopyToPeer(a.OwnFilePath, folderB);

            b.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(17_000));
            await b.Service.RunAsync(CancellationToken.None);
            CopyToPeer(b.OwnFilePath, folderA);

            a.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(18_000));
            await a.Service.RunAsync(CancellationToken.None);

            // Modifiche concorrenti e primo scambio incrociato.
            await a.EditAsync(20_000, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "L");
            await b.EditAsync(30_000, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "W");
            a.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(26_000));
            await a.Service.RunAsync(CancellationToken.None); // fA1 (t=26_000)
            b.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(36_000));
            await b.Service.RunAsync(CancellationToken.None); // fB1 (t=36_000)
            CopyToPeer(a.OwnFilePath, folderB);
            CopyToPeer(b.OwnFilePath, folderA);

            // Entrambi producono il blocco di conflitto C, ma i file fA2 e fB2 non si incrociano ancora.
            a.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(40_000));
            await a.Service.RunAsync(CancellationToken.None); // fA2 (t=40_000)
            var fA2Snapshot = Path.Combine(root, "fA2.rcsync");
            File.Copy(a.OwnFilePath, fA2Snapshot, overwrite: true);

            b.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(41_000));
            await b.Service.RunAsync(CancellationToken.None); // fB2 (t=41_000, merged_from[a]=26_000)

            // Prima di ricevere fA2, B ripulisce il blocco in "W" e scrive fB3; A riceve fB3 prima di aver mai visto fB2.
            await b.EditAsync(60_000, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "W");
            b.Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(66_000));
            await b.Service.RunAsync(CancellationToken.None); // fB3 (t=66_000, merged_from[a]=26_000)

            CopyToPeer(b.OwnFilePath, folderA);
            File.Copy(fA2Snapshot, Path.Combine(folderB, Path.GetFileName(a.OwnFilePath)), overwrite: true);

            var blockReturnCount = 0;
            var hadBlockOnB = false;
            for (var round = 0; round < 4; round++)
            {
                b.Clock.Advance(TimeSpan.FromSeconds(10));
                await b.Service.RunAsync(CancellationToken.None);
                CopyToPeer(b.OwnFilePath, folderA);

                a.Clock.Advance(TimeSpan.FromSeconds(10));
                await a.Service.RunAsync(CancellationToken.None);
                CopyToPeer(a.OwnFilePath, folderB);

                var bodyBNow = (await b.QueryAsync(db => db.Notes.AsNoTracking().SingleAsync(n => n.Id == N))).BodyMd;
                var hasBlockNow = TextConflictBlock.Count(bodyBNow) > 0;
                if (!hadBlockOnB && hasBlockNow) blockReturnCount++;
                hadBlockOnB = hasBlockNow;
            }

            var bodyA = (await a.QueryAsync(db => db.Notes.AsNoTracking().SingleAsync(n => n.Id == N))).BodyMd;
            var bodyB = (await b.QueryAsync(db => db.Notes.AsNoTracking().SingleAsync(n => n.Id == N))).BodyMd;
            blockReturnCount.Should().BeLessThanOrEqualTo(1);
            bodyA.Should().Be(bodyB);
            bodyA.Should().Be("W");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
    }

    private static void CopyToPeer(string sourceFile, string peerFolder) =>
        File.Copy(sourceFile, Path.Combine(peerFolder, Path.GetFileName(sourceFile)), overwrite: true);

    private static async Task<ServiceNode> CreateServiceNodeAsync(string name, string folder, string root, long startMs)
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(startMs));
        var fixture = new DbFixture(clock);
        var deviceId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        fixture.DeviceIdentity.DeviceName = name;
        await using (var db = await fixture.Factory.CreateDbContextAsync())
        {
            db.SyncMeta.Add(new SyncMeta
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                DeviceName = name,
                CreatedAt = clock.UtcNow.UtcDateTime,
            });
            await db.SaveChangesAsync();
        }

        var nodeDir = Path.Combine(root, $"node-{name}");
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
        await settings.SetAsync(SyncService.FolderSettingKey, folder);
        await settings.SetAsync(SyncService.PassphraseSettingKey, "test-sync-phrase");
        await settings.SetAsync(SyncService.JoinedSettingKey, "true");

        var cipher = new SyncFileCipher((passphrase, salt, _, _, _) =>
            Task.FromResult(SHA256.HashData(Encoding.UTF8.GetBytes(passphrase + ":" + Convert.ToHexString(salt)))));
        var identity = new DeviceIdentity(new ServiceNodeFactoryProvider(fixture.Factory));
        var service = new SyncService(
            fixture.Factory,
            settings,
            new NoOpBackupService(),
            paths,
            cipher,
            new SyncRunState(),
            clock,
            identity,
            appVersion: "test",
            machineName: name);

        return new ServiceNode(fixture, clock, service, Path.Combine(folder, SyncService.BuildOwnFileName(name, deviceId)));
    }

    private sealed class ServiceNode(DbFixture fixture, FakeClock clock, SyncService service, string ownFilePath) : IAsyncDisposable
    {
        public FakeClock Clock { get; } = clock;
        public SyncService Service { get; } = service;
        public string OwnFilePath { get; } = ownFilePath;

        public async Task EditAsync(long atMs, Func<RecordumDbContext, Task> edit)
        {
            Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(atMs));
            await using var db = await fixture.Factory.CreateDbContextAsync();
            await edit(db);
            await db.SaveChangesAsync();
        }

        public Task EditAsync(long atMs, Action<RecordumDbContext> edit) =>
            EditAsync(atMs, db => { edit(db); return Task.CompletedTask; });

        public async Task<T> QueryAsync<T>(Func<RecordumDbContext, Task<T>> query)
        {
            await using var db = await fixture.Factory.CreateDbContextAsync();
            return await query(db);
        }

        public ValueTask DisposeAsync() => fixture.DisposeAsync();
    }

    private sealed class ServiceNodeFactoryProvider(IDbContextFactory<RecordumDbContext> factory) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IDbContextFactory<RecordumDbContext>) ? factory : null;
    }

    private sealed class NoOpBackupService : IBackupService
    {
        public Task<BackupResult> CreateAsync(string destinationPath, string passphrase, BackupOptions options, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
