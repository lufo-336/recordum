using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
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
}
