using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures.Sync;
using static Recordum.Data.Tests.Sync.SyncMergerTests;

namespace Recordum.Data.Tests.Sync;

/// <summary>Spec §8.6 "Convergenza": più dispositivi, modifiche e cancellazioni incrociate, giri in ordini diversi →
/// stesso stato ovunque; nessuna riga persa, nessuna cancellata che ritorna.</summary>
public sealed class SyncConvergenceTests
{
    private static readonly Guid H1 = new("22222222-0000-0000-0000-000000000001");
    private static readonly Guid H2 = new("22222222-0000-0000-0000-000000000002");
    private static readonly Guid H3 = new("22222222-0000-0000-0000-000000000003");
    private static readonly Guid P1 = new("22222222-0000-0000-0000-000000000004");
    private static readonly Guid C1 = new("22222222-0000-0000-0000-000000000005");
    private static readonly Guid C2 = new("22222222-0000-0000-0000-000000000006");
    private static readonly Guid T1 = new("22222222-0000-0000-0000-000000000007");

    private static readonly string[] AllOrders = ["abc", "acb", "bac", "bca", "cab", "cba"];

    /// <summary>Con la base fra ogni coppia, i valori dei campi non dipendono dall'ordine dei file (le ore di modifica
    /// possono: una riga combinata prende max+1 dove capita la combinazione).</summary>
    [Fact]
    public async Task ThreeDevicesConvergeInAnyOrder()
    {
        var runs = new List<(string Dump, Facts Facts)>();
        foreach (var order in AllOrders) runs.Add(await ConvergeWithBasesAsync(order));

        runs.Select(r => WithoutTimestamps(r.Dump)).Distinct()
            .Should().ContainSingle("the field values must not depend on the order the files are read in");
        // Cosa deve valere: campi diversi tenuti tutti e due, stesso campo alla più recente, cancellazione più recente
        // della modifica resta, modifica più recente della cancellazione riporta la riga, righe nuove ovunque.
        runs[0].Facts.Should().Be(new Facts(
            H1Name: "Run far", H1Color: "#222222", H2Trashed: true, H3Name: "Write A",
            PauseLeft: false, C1Duration: 90, C2Exists: true, T1Exists: true));
    }

    private sealed record Facts(
        string H1Name, string H1Color, bool H2Trashed, string H3Name, bool PauseLeft, int? C1Duration, bool C2Exists, bool T1Exists);

    private static async Task<(string Dump, Facts Facts)> ConvergeWithBasesAsync(string order)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");
        var devices = new Dictionary<char, SyncDevice> { ['a'] = a, ['b'] = b, ['c'] = c };

        await a.EditAsync(100, db =>
        {
            db.HabitNodes.AddRange(Habit(H1, "Run"), Habit(H2, "Read"), Habit(H3, "Write"));
            db.StreakPauses.Add(Pause(P1, H1));
            db.Completions.Add(Done(C1, H2));
        });
        var files = new Dictionary<char, byte[]>();
        foreach (var d in devices) files[d.Key] = await d.Value.WriteFileAsync();
        for (var i = 0; i < 2; i++) await RoundAsync(devices, files, "abc");

        await a.EditAsync(150, async db => (await db.StreakPauses.SingleAsync(x => x.Id == P1)).Reason = "a");
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H1)).Name = "Run far");
        await a.EditAsync(400, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H3)).Name = "Write A");
        await b.EditAsync(250, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H1)).Color = "#222222");
        await b.EditAsync(260, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H2)).DeletedAt =
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        await b.EditAsync(270, db => db.Completions.Add(Done(C2, H1)));
        await b.EditAsync(280, db => db.Completions.Where(x => x.Id == C1).ExecuteDeleteAsync());
        await c.EditAsync(290, async db => (await db.Completions.SingleAsync(x => x.Id == C1)).DurationSeconds = 90);
        await c.EditAsync(300, db => db.StreakPauses.Where(x => x.Id == P1).ExecuteDeleteAsync());
        await c.EditAsync(310, db => db.Tags.Add(new Tag { Id = T1, Name = "outdoor" }));
        await c.EditAsync(350, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H3)).Name = "Write C");

        await RunToFixedPointAsync(devices, files, order);

        var h1 = await HabitAsync(a, H1);
        var facts = new Facts(
            h1!.Name,
            h1.Color,
            (await HabitAsync(a, H2))!.DeletedAt is not null,
            (await HabitAsync(a, H3))!.Name,
            await a.QueryAsync(db => db.StreakPauses.AnyAsync()),
            (await a.QueryAsync(db => db.Completions.SingleOrDefaultAsync(x => x.Id == C1)))?.DurationSeconds,
            await a.QueryAsync(db => db.Completions.AnyAsync(x => x.Id == C2)),
            await a.QueryAsync(db => db.Tags.AnyAsync(x => x.Id == T1)));
        return (await a.CanonicalDumpAsync(), facts);
    }

    /// <summary>Senza base (§8.4) l'ordine può cambiare quale modifica concorrente della stessa riga resta, ma in
    /// nessun ordine si perde una riga o torna una cancellata.</summary>
    [Fact]
    public async Task WithoutBaseNoRowIsLostInAnyOrder()
    {
        foreach (var order in AllOrders)
        {
            await using var a = new SyncDevice("a");
            await using var b = new SyncDevice("b");
            await using var c = new SyncDevice("c");
            var devices = new Dictionary<char, SyncDevice> { ['a'] = a, ['b'] = b, ['c'] = c };
            await a.EditAsync(100, db =>
            {
                db.HabitNodes.Add(Habit(H1, "Run"));
                db.StreakPauses.Add(Pause(P1, H1));
            });
            var files = new Dictionary<char, byte[]>();
            foreach (var d in devices) files[d.Key] = await d.Value.WriteFileAsync();
            await RoundAsync(devices, files, "abc");
            foreach (var d in devices.Values) await d.ClearBaseAsync();

            await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H1)).Name = "Run far");
            await b.EditAsync(210, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H1)).Color = "#222222");
            await c.EditAsync(220, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H1)).Icon = "star");
            await a.EditAsync(230, db => db.Completions.Add(Done(C1, H1)));
            await b.EditAsync(240, db => db.Completions.Add(Done(C2, H1)));
            await c.EditAsync(250, db => db.Tags.Add(new Tag { Id = T1, Name = "outdoor" }));
            await b.EditAsync(260, db => db.StreakPauses.Where(x => x.Id == P1).ExecuteDeleteAsync());
            files = new Dictionary<char, byte[]>();
            foreach (var d in devices) files[d.Key] = await d.Value.WriteFileAsync();

            await RunToFixedPointAsync(devices, files, order);

            foreach (var d in devices.Values)
            {
                (await HabitAsync(d, H1)).Should().NotBeNull($"order {order}");
                (await d.QueryAsync(db => db.Completions.Select(x => x.Id).ToListAsync()))
                    .Should().BeEquivalentTo([C1, C2], $"order {order}");
                (await d.QueryAsync(db => db.Tags.AnyAsync(x => x.Id == T1))).Should().BeTrue($"order {order}");
                (await d.QueryAsync(db => db.StreakPauses.AnyAsync())).Should().BeFalse($"order {order}");
            }
        }
    }

    private static string WithoutTimestamps(string dump) =>
        System.Text.RegularExpressions.Regex.Replace(dump, "updated_at_ms=-?[0-9]+", "updated_at_ms=*");


    [Fact]
    public async Task RelayThroughIntermediary()
    {
        // Cartella condivisa: A e B. Il telefono legge solo il file di A e A legge quello del telefono.
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var phone = new SyncDevice("phone");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H1, "Run"));
            db.StreakPauses.Add(Pause(P1, H1));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await phone.MergeAsync(await a.WriteFileAsync());

        await b.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(x => x.Id == H1)).Name = "Run far");
        await b.EditAsync(210, db => db.StreakPauses.Where(x => x.Id == P1).ExecuteDeleteAsync());
        await phone.EditAsync(220, db => db.Completions.Add(Done(C1, H1)));

        for (var round = 0; round < 3; round++)
        {
            await a.MergeAsync(await b.WriteFileAsync());
            await a.MergeAsync(await phone.WriteFileAsync());
            var fa = await a.WriteFileAsync();
            await phone.MergeAsync(fa);
            await b.MergeAsync(fa);
        }

        (await HabitAsync(phone, H1))!.Name.Should().Be("Run far", "B's edit reaches the phone through A");
        (await phone.QueryAsync(db => db.StreakPauses.AnyAsync())).Should().BeFalse("and so does B's delete");
        (await b.QueryAsync(db => db.Completions.AnyAsync(x => x.Id == C1))).Should().BeTrue("the phone's record reaches B");
        var dump = await a.CanonicalDumpAsync();
        (await b.CanonicalDumpAsync()).Should().Be(dump);
        (await phone.CanonicalDumpAsync()).Should().Be(dump);
    }

    /// <summary>Un giro: ogni dispositivo, nell'ordine dato, legge i file più recenti degli altri e riscrive il suo.</summary>
    private static async Task RoundAsync(Dictionary<char, SyncDevice> devices, Dictionary<char, byte[]> files, string order)
    {
        foreach (var me in order)
        {
            foreach (var other in order.Where(o => o != me))
                await devices[me].MergeAsync(files[other]);
            files[me] = await devices[me].WriteFileAsync();
        }
    }

    private static async Task RunToFixedPointAsync(
        Dictionary<char, SyncDevice> devices, Dictionary<char, byte[]> files, string order)
    {
        const int maxRounds = 6;
        string[] dumps = [];
        for (var round = 1; round <= maxRounds; round++)
        {
            await RoundAsync(devices, files, order);
            var next = new string[order.Length];
            for (var i = 0; i < order.Length; i++) next[i] = await devices[order[i]].CanonicalDumpAsync();
            if (next.Distinct().Count() == 1 && dumps.SequenceEqual(next)) return;
            dumps = next;
        }

        dumps.Distinct().Should().ContainSingle($"devices must reach the same state within {maxRounds} rounds");
        throw new InvalidOperationException($"no fixed point within {maxRounds} rounds");
    }
}
