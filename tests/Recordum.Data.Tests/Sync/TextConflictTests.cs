using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures.Sync;
using static Recordum.Data.Tests.Sync.SyncMergerTests;

namespace Recordum.Data.Tests.Sync;

/// <summary>Regola B (Task 7): due modifiche concorrenti su note o annotazioni si tengono tutte e due in coda,
/// convergono anche con fusi orari o orologi sfasati e non vanno perse nemmeno contro una cancellazione.</summary>
public sealed class TextConflictTests
{
    private static readonly Guid H = new("33333333-0000-0000-0000-000000000001");
    private static readonly Guid N = new("33333333-0000-0000-0000-000000000002");
    private static readonly Guid C = new("33333333-0000-0000-0000-000000000003");

    private static Note QuickNote(Guid id, Guid habit, string body, DateOnly? day = null) => new()
    {
        Id = id,
        HabitNodeId = habit,
        Category = NoteCategory.Veloce,
        TargetDate = day ?? new DateOnly(2026, 11, 22),
        BodyMd = body,
    };

    private static Task<Note?> NoteAsync(SyncDevice d, Guid id) =>
        d.QueryAsync(db => db.Notes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == id));

    [Fact]
    public async Task BothEditedKeepsBothWithBlock()
    {
        var rome = TimeZoneInfo.CreateCustomTimeZone("CET", TimeSpan.FromHours(1), "CET", "CET");
        var baseMs = new DateTimeOffset(2026, 11, 22, 20, 0, 0, TimeSpan.FromHours(1)).ToUnixTimeMilliseconds();
        var phoneMs = new DateTimeOffset(2026, 11, 22, 21, 40, 0, TimeSpan.FromHours(1)).ToUnixTimeMilliseconds();
        var desktopMs = new DateTimeOffset(2026, 11, 22, 21, 50, 0, TimeSpan.FromHours(1)).ToUnixTimeMilliseconds();

        await using var phone = new SyncDevice("Telefono", baseMs, rome);
        await using var desktop = new SyncDevice("Desktop", baseMs, rome);
        await desktop.EditAsync(baseMs, db =>
        {
            db.HabitNodes.Add(Habit(H, "Diario"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        await phone.MergeAsync(await desktop.WriteFileAsync());

        await phone.EditAsync(phoneMs, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "dal telefono");
        await desktop.EditAsync(desktopMs, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "dal desktop");

        var report = await desktop.MergeAsync(await phone.WriteFileAsync());

        report.TextConflicts.Should().Be(1);
        var note = await NoteAsync(desktop, N);
        note!.BodyMd.Should().Be("dal desktop\n\n— Telefono · 2026-11-22 21:40 —\ndal telefono");
        note.UpdatedAtMs.Should().Be(desktopMs + 1, "a combined text is new on both sides: max + 1, like every merged row that differs from the remote");
        TextConflictBlock.Count(note.BodyMd).Should().Be(1);
        TextConflictBlock.Contains(note.BodyMd, "dal telefono").Should().BeTrue();
    }

    /// <summary><c>last_edited_device_name</c> segue la versione in cima, anche quando solo l'altra parte l'ha
    /// cambiato rispetto alla base: è il nome che il blocco userà se quella versione perderà più avanti.</summary>
    [Fact]
    public async Task DeviceNameFollowsTheTopVersion()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        await b.EditAsync(200, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "from b");
        await a.EditAsync(300, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "from a");
        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);
        await b.MergeAsync(fa);

        foreach (var d in new[] { a, b })
        {
            var note = (await NoteAsync(d, N))!;
            note.BodyMd.Should().Be("from a\n\n— b · 1970-01-01 00:00 —\nfrom b");
            note.LastEditedDeviceName.Should().Be("a", $"on {d.Name}: the version at the top is a's");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task OneSideEditedNoBlock()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        await b.MergeAsync(await a.WriteFileAsync());

        await a.EditAsync(200, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "only a edited");
        await b.EditAsync(300, async db => (await db.Notes.SingleAsync(n => n.Id == N)).IsSensitive = true);

        var report = await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        report.TextConflicts.Should().Be(0);
        foreach (var d in new[] { a, b })
        {
            var note = await NoteAsync(d, N);
            note!.BodyMd.Should().Be("only a edited");
            note.IsSensitive.Should().BeTrue();
            TextConflictBlock.Count(note.BodyMd).Should().Be(0);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThreeDevicesConvergeToSameText(bool withBase)
    {
        string[] orders = ["abc", "acb", "bac", "bca", "cab", "cba"];
        var t0 = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var tA = new DateTimeOffset(2026, 11, 22, 11, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var tB = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var tC = new DateTimeOffset(2026, 11, 22, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var results = new List<string>();
        foreach (var order in orders)
        {
            await using var a = new SyncDevice("a", t0);
            await using var b = new SyncDevice("b", t0);
            await using var c = new SyncDevice("c", t0);
            var devices = new Dictionary<char, SyncDevice> { ['a'] = a, ['b'] = b, ['c'] = c };

            await a.EditAsync(t0, db =>
            {
                db.HabitNodes.Add(Habit(H, "Journal"));
                db.Notes.Add(QuickNote(N, H, "base"));
            });
            var files = new Dictionary<char, byte[]>();
            foreach (var d in devices) files[d.Key] = await d.Value.WriteFileAsync();
            for (var r = 0; r < 2; r++)
            {
                foreach (var me in "abc")
                {
                    foreach (var other in "abc".Where(o => o != me))
                        await devices[me].MergeAsync(files[other]);
                    files[me] = await devices[me].WriteFileAsync();
                }
            }

            if (!withBase)
            {
                foreach (var d in devices.Values) await d.ClearBaseAsync();
            }

            await a.EditAsync(tA, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "version A");
            await b.EditAsync(tB, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "version B");
            await c.EditAsync(tC, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "version C");
            foreach (var d in devices) files[d.Key] = await d.Value.WriteFileAsync();

            for (var r = 0; r < 3; r++)
            {
                foreach (var me in order)
                {
                    foreach (var other in order.Where(o => o != me))
                        await devices[me].MergeAsync(files[other]);
                    files[me] = await devices[me].WriteFileAsync();
                }
            }

            var dump = await a.CanonicalDumpAsync();
            (await b.CanonicalDumpAsync()).Should().Be(dump, $"order {order}");
            (await c.CanonicalDumpAsync()).Should().Be(dump, $"order {order}");

            var body = (await NoteAsync(a, N))!.BodyMd;
            body.Should().StartWith("version C");
            foreach (var v in new[] { "version A", "version B", "version C" })
                Occurrences(body, v).Should().Be(1, $"order {order}: {v} exactly once");
            TextConflictBlock.Count(body).Should().Be(2);
            results.Add(body);
        }

        results.Distinct().Should().ContainSingle("all merge orders must produce the exact same text");
    }

    [Fact]
    public async Task NoDuplicateBlockOnRemerge()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.EditAsync(200, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "from a");
        await b.EditAsync(300, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "from b");
        var fa = await a.WriteFileAsync();

        var first = await b.MergeAsync(fa);
        var bodyAfterFirst = (await NoteAsync(b, N))!.BodyMd;
        var second = await b.MergeAsync(fa);

        first.TextConflicts.Should().Be(1);
        second.TextConflicts.Should().Be(0);
        (await NoteAsync(b, N))!.BodyMd.Should().Be(bodyAfterFirst);
        TextConflictBlock.Count(bodyAfterFirst).Should().Be(1);

        // Anche senza base (es. un giro tramite un terzo dispositivo) la stessa versione non si riaggiunge.
        await b.ClearBaseAsync();
        var third = await b.MergeAsync(fa);
        third.TextConflicts.Should().Be(0);
        (await NoteAsync(b, N))!.BodyMd.Should().Be(bodyAfterFirst);
        TextConflictBlock.Count(bodyAfterFirst).Should().Be(1);
    }

    /// <summary>Un testo già unito altrove (W in cima, L nel blocco) arriva su un dispositivo che ha W, L o un'altra
    /// versione Z, con la base o senza: ogni versione resta una volta sola.</summary>
    [Theory]
    [InlineData("W", true)]
    [InlineData("W", false)]
    [InlineData("L", true)]
    [InlineData("L", false)]
    [InlineData("Z", true)]
    [InlineData("Z", false)]
    public async Task NoDuplicateBlockWhenCombinedTextArrives(string local, bool withBase)
    {
        await using var a = new SyncDevice("a"); // ha L, unisce W: tiene "W + blocco(L)"
        await using var p = new SyncDevice("p"); // ha scritto W
        await using var x = new SyncDevice("x"); // un terzo, per L e Z
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        var start = await a.WriteFileAsync();
        await p.MergeAsync(start);
        await x.MergeAsync(start);

        await a.EditAsync(200, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "L");
        var withL = await a.WriteFileAsync();
        await p.EditAsync(300, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "W");
        await a.MergeAsync(await p.WriteFileAsync());
        var combined = await a.WriteFileAsync();
        var combinedBody = (await NoteAsync(a, N))!.BodyMd;
        combinedBody.Should().Be("W\n\n— a · 1970-01-01 00:00 —\nL");

        var target = local switch { "W" => p, _ => x };
        if (local == "L") await x.MergeAsync(withL);
        if (local == "Z") await x.EditAsync(250, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "Z");
        if (!withBase) await target.ClearBaseAsync();

        await target.MergeAsync(combined);

        var body = (await NoteAsync(target, N))!.BodyMd;
        var expected = local == "Z" ? new[] { "W", "L", "Z" } : ["W", "L"];
        body.Should().StartWith("W\n\n");
        foreach (var v in expected)
            Occurrences(body, v).Should().Be(1, $"{v} once in {body}");
        TextConflictBlock.Count(body).Should().Be(expected.Length - 1);
        if (local != "Z") body.Should().Be(combinedBody, "nothing new to add: the combined text is taken as it is");

        // E al giro dopo i due restano uguali, senza copie.
        await a.MergeAsync(await target.WriteFileAsync());
        await target.MergeAsync(await a.WriteFileAsync());
        (await NoteAsync(a, N))!.BodyMd.Should().Be(body);
        (await NoteAsync(target, N))!.BodyMd.Should().Be(body);
    }

    private static int Occurrences(string body, string version) =>
        body.Split('\n').Count(line => line == version);

    [Fact]
    public async Task SkewedClockLosesNoText()
    {
        var realT = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var skewedT = new DateTimeOffset(2026, 11, 22, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(); // +3h avanti

        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        await b.MergeAsync(await a.WriteFileAsync());

        await a.EditAsync(realT, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "written at real 10:00");
        await b.EditAsync(skewedT, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "written with +3h skewed clock");

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);
        await b.MergeAsync(fa);

        foreach (var d in new[] { a, b })
        {
            var body = (await NoteAsync(d, N))!.BodyMd;
            body.Should().StartWith("written with +3h skewed clock");
            body.Should().Contain("written at real 10:00");
            TextConflictBlock.Count(body).Should().Be(1);
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task DeleteVsTextEditKeepsText()
    {
        var day = new DateOnly(2026, 11, 22);
        var editMs = new DateTimeOffset(2026, 11, 22, 21, 40, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var deleteMs = editMs + 60_000;

        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Journal"));
            db.Notes.Add(QuickNote(N, H, "initial", day));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        await b.EditAsync(editMs, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "important unsaved words");
        await a.EditAsync(deleteMs, db => db.Notes.Where(n => n.Id == N).ExecuteDeleteAsync());

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);
        await b.MergeAsync(fa);

        foreach (var d in new[] { a, b })
        {
            (await NoteAsync(d, N)).Should().BeNull("the deleted note stays deleted");
            var notes = await d.QueryAsync(db => db.Notes.AsNoTracking().ToListAsync());
            notes.Should().ContainSingle("the losing text is rescued into one new quick note");
            var rescued = notes[0];
            rescued.Id.Should().NotBe(N);
            rescued.HabitNodeId.Should().Be(H);
            rescued.Category.Should().Be(NoteCategory.Veloce);
            rescued.TargetDate.Should().Be(day);
            rescued.BodyMd.Should().Be("— b · 2026-11-22 21:40 —\nimportant unsaved words");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    /// <summary>Una cancellazione qualunque (nessuno ha scritto altro) non fa nascere note: chi l'ha cancellata
    /// rilegge un file che porta ancora la nota (anche senza base: è la riga che aveva ricevuto), l'altro riceve la
    /// lapide e la nota com'era nella sua base. Il caso che resta fuori (l'altro non ha mai unito un file del cancellatore
    /// con quella nota, quindi non ha base) salva il testo: senza base non si sa se l'altro l'aveva (spec §8.4).</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PlainDeleteRescuesNothing(bool deletedByCreator, bool creatorHasBase)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Journal"));
            db.Notes.Add(QuickNote(N, H, "hello"));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        if (creatorHasBase) await a.MergeAsync(await b.WriteFileAsync());
        var deleter = deletedByCreator ? a : b;
        var other = deletedByCreator ? b : a;
        var stale = await other.WriteFileAsync(); // la nota c'è ancora
        await deleter.EditAsync(200, db => db.Notes.Where(n => n.Id == N).ExecuteDeleteAsync());

        await deleter.MergeAsync(stale);
        await other.MergeAsync(await deleter.WriteFileAsync());
        await deleter.MergeAsync(await other.WriteFileAsync());

        foreach (var d in new[] { a, b })
            (await d.QueryAsync(db => db.Notes.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task DeletedCompletionKeepsItsEditedNotes()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Completions.Add(Done(C, H));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        await b.EditAsync(200, async db => (await db.Completions.SingleAsync(x => x.Id == C)).Notes = "felt great");
        await a.EditAsync(300, db => db.Completions.Where(x => x.Id == C).ExecuteDeleteAsync());
        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);
        await b.MergeAsync(fa);

        var completedAt = Done(C, H).CompletedAt;
        foreach (var d in new[] { a, b })
        {
            (await d.QueryAsync(db => db.Completions.CountAsync())).Should().Be(0);
            var rescued = (await d.QueryAsync(db => db.Notes.AsNoTracking().ToListAsync())).Should().ContainSingle().Subject;
            rescued.HabitNodeId.Should().Be(H);
            rescued.Category.Should().Be(NoteCategory.Veloce);
            rescued.TargetDate.Should().BeNull();
            rescued.CreatedAt.Should().Be(completedAt, "the same day as the completion");
            rescued.BodyMd.Should().Be("— b · 1970-01-01 00:00 —\nfelt great");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task RescuedNoteDeletedStaysDeleted()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Journal"));
            db.Notes.Add(QuickNote(N, H, "initial"));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());
        await b.EditAsync(200, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "edited on b");
        await a.EditAsync(300, db => db.Notes.Where(n => n.Id == N).ExecuteDeleteAsync());
        var fb = await b.WriteFileAsync();

        await a.MergeAsync(fb);
        var rescued = (await a.QueryAsync(db => db.Notes.AsNoTracking().ToListAsync())).Should().ContainSingle().Subject;
        await a.EditAsync(400, db => db.Notes.Where(n => n.Id == rescued.Id).ExecuteDeleteAsync());

        await a.MergeAsync(fb); // b non ha ancora visto niente: la sua riga modificata arriva di nuovo
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        foreach (var d in new[] { a, b })
            (await d.QueryAsync(db => db.Notes.CountAsync())).Should().Be(0, "the user deleted the rescued note");
    }

    [Fact]
    public async Task ConflictCountReported()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "note base"));
            db.Completions.Add(Done(C, H));
        });
        await b.MergeAsync(await a.WriteFileAsync());

        await a.EditAsync(200, async db =>
        {
            (await db.HabitNodes.SingleAsync(h => h.Id == H)).Name = "Run A";
            (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "note A";
            (await db.Completions.SingleAsync(c => c.Id == C)).Notes = "comp A";
        });
        await b.EditAsync(300, async db =>
        {
            (await db.HabitNodes.SingleAsync(h => h.Id == H)).Name = "Run B";
            (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "note B";
            (await db.Completions.SingleAsync(c => c.Id == C)).Notes = "comp B";
        });

        var fa = await a.WriteFileAsync();
        var report = await b.MergeAsync(fa);
        report.TextConflicts.Should().Be(2);

        var second = await b.MergeAsync(fa);
        second.TextConflicts.Should().Be(0);
    }

    [Fact]
    public async Task DifferentTimeZonesConvergeAcrossRounds()
    {
        var utc = TimeZoneInfo.Utc;
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("EET", TimeSpan.FromHours(2), "EET", "EET");
        var t0 = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var tA = new DateTimeOffset(2026, 11, 22, 11, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var tB = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await using var a = new SyncDevice("a", t0, utc);
        await using var b = new SyncDevice("b", t0, plusTwo);
        await a.EditAsync(t0, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(QuickNote(N, H, "base"));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        await a.EditAsync(tA, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "from a");
        await b.EditAsync(tB, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = "from b");

        for (var round = 0; round < 3; round++)
        {
            var fa = await a.WriteFileAsync();
            var fb = await b.WriteFileAsync();
            await a.MergeAsync(fb);
            await b.MergeAsync(fa);
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
        var body = (await NoteAsync(a, N))!.BodyMd;
        body.Should().StartWith("from b").And.Contain("from a");
        TextConflictBlock.Count(body).Should().Be(1);
    }

    [Fact]
    public void ContainsMatchesExactBlockBodyOnly()
    {
        const string text = "winner\n\n— ? · 2026-11-22 21:40 —\nfoo bar";

        TextConflictBlock.Contains(text, "foo bar").Should().BeTrue();
        TextConflictBlock.Contains(text, "foo").Should().BeFalse();
        TextConflictBlock.Merge("", "foo", null, 1_795_383_600_000, TimeZoneInfo.Utc)
            .Should().StartWith("— ? · ");
    }
}
