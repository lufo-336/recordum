using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Models;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures.Sync;

namespace Recordum.Data.Tests.Sync;

/// <summary>Il motore d'unione del Task 6: righe nuove, campi, lapidi, base per dispositivo, storici, idempotenza.</summary>
public sealed class SyncMergerTests
{
    private static readonly Guid H = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid P = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid C = new("11111111-0000-0000-0000-000000000003");
    private static readonly Guid N = new("11111111-0000-0000-0000-000000000004");
    private static readonly Guid At = new("11111111-0000-0000-0000-000000000005");

    internal static HabitNode Habit(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NodeType = NodeType.Habit,
        HabitType = HabitType.Action,
        Color = "#808080",
    };

    internal static StreakPause Pause(Guid id, Guid habit) => new()
    {
        Id = id,
        HabitNodeId = habit,
        PauseFrom = new DateOnly(2026, 1, 1),
        PauseTo = new DateOnly(2026, 1, 2),
        Reason = "start",
    };

    internal static Completion Done(Guid id, Guid habit) => new()
    {
        Id = id,
        HabitNodeId = habit,
        CompletedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        CompletedAtMs = 1_767_268_800_000,
        Notes = "base",
    };

    internal static Task<HabitNode?> HabitAsync(SyncDevice d, Guid id) =>
        d.QueryAsync(db => db.HabitNodes.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(n => n.Id == id));

    private static Task<StreakPause?> PauseAsync(SyncDevice d) =>
        d.QueryAsync(db => db.StreakPauses.AsNoTracking().SingleOrDefaultAsync(p => p.Id == P));

    private static Task<List<SyncTombstone>> TombstonesAsync(SyncDevice d) =>
        d.QueryAsync(db => db.SyncTombstones.AsNoTracking().ToListAsync());

    /// <summary>A crea H (e P), B unisce il file di A: B ha la base per A.</summary>
    private static async Task ShareAsync(SyncDevice a, SyncDevice b, bool withPause = false)
    {
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            if (withPause) db.StreakPauses.Add(Pause(P, H));
        });
        await b.MergeAsync(await a.WriteFileAsync());
    }

    private static async Task ExchangeAsync(SyncDevice a, SyncDevice b)
    {
        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);
        await b.MergeAsync(fa);
    }

    [Fact]
    public async Task NewRowInserted()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db => db.HabitNodes.Add(Habit(H, "Run")));

        var report = await b.MergeAsync(await a.WriteFileAsync());

        report.Inserted.Should().Be(1);
        (await HabitAsync(b, H))!.Name.Should().Be("Run");
        (await b.CanonicalDumpAsync()).Should().Be(await a.CanonicalDumpAsync());
    }

    [Fact]
    public async Task RemoteNewerFieldWins()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b);
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run far");

        var report = await b.MergeAsync(await a.WriteFileAsync());

        report.Updated.Should().Be(1);
        var h = await HabitAsync(b, H);
        h!.Name.Should().Be("Run far");
        h.UpdatedAtMs.Should().Be(200);
    }

    [Fact]
    public async Task DifferentFieldsChangedOnBothSidesBothKept()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b);
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Color = "#111111");
        await b.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");

        await b.MergeAsync(await a.WriteFileAsync());

        var h = await HabitAsync(b, H);
        h!.Name.Should().Be("Walk", "only B changed the name since the base");
        h.Color.Should().Be("#111111", "only A changed the colour since the base, even though B's row is newer");

        await a.MergeAsync(await b.WriteFileAsync());
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task CombinedRowWinsOnPeerWithoutBase()
    {
        // A ha la base per B, B non ce l'ha per A (non ha mai letto il file di A). A combina un campo suo e uno di B:
        // la riga combinata deve battere la versione di B anche da B, che non ha base e decide a riga intera.
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");
        await c.EditAsync(100, db => db.HabitNodes.Add(Habit(H, "Run")));
        var fc = await c.WriteFileAsync();
        await b.MergeAsync(fc);
        await a.MergeAsync(fc);
        await a.MergeAsync(await b.WriteFileAsync());
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Color = "#EEEEEE");
        await b.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");

        await a.MergeAsync(await b.WriteFileAsync());
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
        var h = await HabitAsync(b, H);
        h!.Name.Should().Be("Walk");
        h.Color.Should().Be("#EEEEEE");
    }

    [Fact]
    public async Task WithoutBaseNewerRowWins()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b);
        await b.ClearBaseAsync();
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Color = "#111111");
        await b.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");

        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        foreach (var d in new[] { a, b })
        {
            var h = await HabitAsync(d, H);
            h!.Name.Should().Be("Walk");
            h.Color.Should().Be("#808080", "without a base the whole newer row wins (the documented ceiling)");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task TieBrokenDeterministically()
    {
        // Stessa ora, stesso campo. A ha il device_id minore ma "Beta": vince la forma canonica minore ("Alpha"),
        // non il device_id.
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b);
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Beta");
        await b.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Alpha");

        await ExchangeAsync(a, b);

        (await HabitAsync(a, H))!.Name.Should().Be("Alpha");
        (await HabitAsync(b, H))!.Name.Should().Be("Alpha");
        await ExchangeAsync(a, b);
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task LocalDeleteNewerThanRemoteEditStaysDeleted()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b, withPause: true);
        await a.EditAsync(200, async db => (await db.StreakPauses.SingleAsync(p => p.Id == P)).Reason = "edited");
        await b.EditAsync(300, db => db.StreakPauses.Where(p => p.Id == P).ExecuteDeleteAsync());

        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        foreach (var d in new[] { a, b })
        {
            (await PauseAsync(d)).Should().BeNull();
            (await TombstonesAsync(d)).Should().ContainSingle(t => t.Entity == "streak_pause" && t.DeletedAtMs == 300);
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task RemoteEditNewerThanLocalDeleteResurrects()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b, withPause: true);
        await b.EditAsync(200, db => db.StreakPauses.Where(p => p.Id == P).ExecuteDeleteAsync());
        await a.EditAsync(300, async db => (await db.StreakPauses.SingleAsync(p => p.Id == P)).Reason = "edited");

        // Tutte e due leggono il file dell'altra scritto prima di unire: ognuna vede la mossa dell'altra a freddo.
        await ExchangeAsync(a, b);
        await ExchangeAsync(a, b);

        foreach (var d in new[] { a, b })
        {
            (await PauseAsync(d))!.Reason.Should().Be("edited");
            (await TombstonesAsync(d)).Should().BeEmpty("a row newer than its tombstone beats it everywhere");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task HabitRestoredFromTrashWins()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b);
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).DeletedAt =
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        await b.MergeAsync(await a.WriteFileAsync());
        (await HabitAsync(b, H))!.DeletedAt.Should().NotBeNull("the trash travels as a normal field");

        await b.EditAsync(300, async db =>
            (await db.HabitNodes.IgnoreQueryFilters().SingleAsync(n => n.Id == H)).DeletedAt = null);
        await a.MergeAsync(await b.WriteFileAsync());

        (await HabitAsync(a, H))!.DeletedAt.Should().BeNull();
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task ParentDeletedChildrenFollow()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Completions.Add(Done(C, H));
        });
        await b.MergeAsync(await a.WriteFileAsync());

        // A svuota il cestino (figli prima: le FK sono RESTRICT). B, dopo, modifica la registrazione e scrive una nota.
        await a.EditAsync(200, async db =>
        {
            await db.Completions.Where(x => x.Id == C).ExecuteDeleteAsync();
            await db.HabitNodes.IgnoreQueryFilters().Where(x => x.Id == H).ExecuteDeleteAsync();
        });
        await b.EditAsync(300, async db =>
        {
            (await db.Completions.SingleAsync(x => x.Id == C)).DurationSeconds = 60;
            db.Notes.Add(new Note { Id = N, HabitNodeId = H, BodyMd = "later" });
        });

        // Tutte e due leggono il file dell'altra scritto prima di unire: ognuna vede la mossa dell'altra a freddo.
        await ExchangeAsync(a, b);
        await ExchangeAsync(a, b);

        foreach (var d in new[] { a, b })
        {
            (await HabitAsync(d, H)).Should().BeNull();
            (await d.QueryAsync(db => db.Completions.CountAsync())).Should().Be(0, "a child of a deleted parent follows it");
            (await d.QueryAsync(db => db.Notes.CountAsync())).Should().Be(0);
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task HistoryRowsAreUnion()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        var h1 = Guid.NewGuid();
        var h2 = Guid.NewGuid();
        var h3 = Guid.NewGuid();
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.HabitConfigHistories.Add(History(h1));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.EditAsync(200, db => db.HabitConfigHistories.Add(History(h2)));
        await b.EditAsync(200, db => db.HabitConfigHistories.Add(History(h3)));
        await a.EditAsync(300, async db => (await db.HabitConfigHistories.SingleAsync(x => x.Id == h1)).Repeatable = true);

        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        foreach (var d in new[] { a, b })
            (await d.QueryAsync(db => db.HabitConfigHistories.Select(x => x.Id).ToListAsync()))
                .Should().BeEquivalentTo([h1, h2, h3]);
        (await b.QueryAsync(db => db.HabitConfigHistories.SingleAsync(x => x.Id == h1))).Repeatable
            .Should().BeFalse("a history row is never updated by a merge, only added");

        static HabitConfigHistory History(Guid id) => new()
        {
            Id = id,
            HabitNodeId = H,
            ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    [Fact]
    public async Task MergeIsIdempotent()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b, withPause: true);
        await b.EditAsync(150, db => db.HabitNodes.Add(Habit(N, "B's own")));
        await a.EditAsync(200, async db =>
        {
            (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run far";
            db.Completions.Add(Done(C, H));
            await db.StreakPauses.Where(p => p.Id == P).ExecuteDeleteAsync();
        });
        var fa = await a.WriteFileAsync();

        var first = await b.MergeAsync(fa);
        var dump = await b.CanonicalDumpAsync();
        var second = await b.MergeAsync(fa);

        first.Should().Be(new SyncMergeReport(Inserted: 1, Updated: 1, Deleted: 1, TextConflicts: 0, Repaired: 0));
        second.Should().Be(new SyncMergeReport(0, 0, 0, 0, 0));
        (await b.CanonicalDumpAsync()).Should().Be(dump);
    }

    [Fact]
    public async Task StaleFileDoesNotRegress()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.StreakPauses.Add(Pause(P, H));
        });
        var stale = await a.WriteFileAsync();
        await a.EditAsync(200, async db =>
        {
            (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run far";
            await db.StreakPauses.Where(p => p.Id == P).ExecuteDeleteAsync();
        });
        await b.MergeAsync(await a.WriteFileAsync());
        var dump = await b.CanonicalDumpAsync();

        var report = await b.MergeAsync(stale);

        report.Should().Be(new SyncMergeReport(0, 0, 0, 0, 0));
        (await b.CanonicalDumpAsync()).Should().Be(dump);
        (await HabitAsync(b, H))!.Name.Should().Be("Run far");
    }

    [Fact]
    public async Task FailureRollsBackEverything()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await ShareAsync(a, b, withPause: true);
        await a.EditAsync(200, async db =>
        {
            (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run far";
            db.Completions.Add(Done(C, H));
            await db.StreakPauses.Where(p => p.Id == P).ExecuteDeleteAsync();
        });
        var fa = await a.WriteFileAsync();
        var dump = await b.CanonicalDumpAsync();
        var bases = await b.QueryAsync(db => db.SyncBases.AsNoTracking().Select(x => x.RowJson).ToListAsync());

        var merge = () => b.MergeAsync(fa, afterEntity: e =>
        {
            if (e == "habit_node") throw new InvalidOperationException("injected");
        });

        await merge.Should().ThrowAsync<InvalidOperationException>().WithMessage("injected");
        (await b.CanonicalDumpAsync()).Should().Be(dump);
        (await b.QueryAsync(db => db.SyncBases.AsNoTracking().Select(x => x.RowJson).ToListAsync()))
            .Should().BeEquivalentTo(bases);

        // Il flag "sto applicando" è spento: una modifica dell'utente torna a lasciare la sua lapide.
        await b.EditAsync(400, db => db.StreakPauses.Where(p => p.Id == P).ExecuteDeleteAsync());
        (await TombstonesAsync(b)).Should().ContainSingle(t => t.DeletedAtMs == 400);
    }

    [Fact]
    public async Task ReceivePhotosOffKeepsThumbnailAndTheOriginalSurvivesElsewhere()
    {
        await using var a = new SyncDevice("a");
        await using var phone = new SyncDevice("phone");
        var content = SyncSeed.CreateSamplePhotoBytes();
        var thumb = SyncSeed.CreateSampleThumbnailBytes();
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Notes.Add(new Note { Id = N, HabitNodeId = H, BodyMd = "photo" });
            db.Attachments.Add(new Recordum.Data.Models.Attachment
            {
                Id = At, NoteId = N, Filename = "p.jpg", MimeType = "image/jpeg",
                FileSizeBytes = content.Length, Content = content, Thumbnail = thumb,
            });
        });

        await phone.MergeAsync(await a.WriteFileAsync(), receivePhotos: false);
        var onPhone = await phone.QueryAsync(db => db.Attachments.AsNoTracking().SingleAsync());
        onPhone.Content.Should().BeNull("the phone does not keep others' originals");
        onPhone.Thumbnail.Should().Equal(thumb);

        await a.MergeAsync(await phone.WriteFileAsync(includePhotos: false));
        (await a.QueryAsync(db => db.Attachments.AsNoTracking().SingleAsync())).Content
            .Should().Equal(content, "a photo without its original is not a change: whoever has it keeps it");
    }

    [Fact]
    public async Task PeerCannotSetLocalOnlySettings()
    {
        // Un file fabbricato a mano: chiavi di dispositivo (privacy, sync, layout) e una lapide su "language".
        // Solo le chiavi che viaggiano (daily_reset_time qui) possono cambiare.
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        var before = await SettingsAsync(b);
        var forged = Rewrite(await a.WriteZipAsync(), "data/app_setting.jsonl", lines =>
        {
            var template = JsonNode.Parse(lines.First(l => l.Contains("\"daily_reset_time\"", StringComparison.Ordinal)))!;
            string Row(string key, string value)
            {
                var o = template.DeepClone().AsObject();
                o["id"] = key;
                o["key"] = key;
                o["value"] = value;
                o["updated_at_ms"] = 9_999_999L;
                return o.ToJsonString();
            }
            return [Row("ai_default_sharing", "cloud"), Row("ai_mode", "cloud"), Row("daily_reset_time", "05:00"),
                Row("home.rows", "[]"), Row("sync.passphrase", "stolen")];
        });
        forged = Rewrite(forged, "tombstones.jsonl", _ =>
            ["{\"entity\":\"app_setting\",\"entity_id\":\"language\",\"deleted_at_ms\":9999999,\"device_id\":null,\"alias_of\":null}"]);

        await b.MergeAsync(await SyncDevice.SealAsync(forged));

        var after = await SettingsAsync(b);
        after["daily_reset_time"].Should().Be("05:00", "a travelling key does merge");
        after.Remove("daily_reset_time");
        before.Remove("daily_reset_time");
        after.Should().BeEquivalentTo(before, "local-only keys are never written, created or deleted by a peer");

        static Task<Dictionary<string, string?>> SettingsAsync(SyncDevice d) =>
            d.QueryAsync(db => db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => (string?)s.Value));
    }

    [Fact]
    public async Task ChildOfDeletedParentWithSetNullLosesTheReference()
    {
        // scale_anchor.tag_id è SET NULL: B aggancia uno scaglione a un tag che A ha già cancellato.
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        var scale = Guid.NewGuid();
        var anchor = Guid.NewGuid();
        await a.EditAsync(100, db =>
        {
            db.ScaleTemplates.Add(new ScaleTemplate { Id = scale, Code = "mood-x", Name = "Mood", MinValue = 1, MaxValue = 5 });
            db.Tags.Add(new Tag { Id = N, Name = "calm" });
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.EditAsync(200, db => db.Tags.Where(t => t.Id == N).ExecuteDeleteAsync());
        await b.EditAsync(300, db =>
            db.ScaleAnchors.Add(new ScaleAnchor { Id = anchor, ScaleTemplateId = scale, Value = 3, Label = "ok", TagId = N }));

        await ExchangeAsync(a, b);
        await ExchangeAsync(a, b);

        foreach (var d in new[] { a, b })
        {
            var x = await d.QueryAsync(db => db.ScaleAnchors.AsNoTracking().SingleAsync(s => s.Id == anchor));
            x.TagId.Should().BeNull("the reference follows the deleted parent as the local SET NULL would");
            (await d.QueryAsync(db => db.Tags.AnyAsync(t => t.Id == N))).Should().BeFalse();
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    private static byte[] Rewrite(byte[] zip, string entryName, Func<List<string>, IEnumerable<string>> edit)
    {
        using var ms = new MemoryStream();
        ms.Write(zip);
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(entryName)!;
            List<string> lines;
            using (var reader = new StreamReader(entry.Open()))
                lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open(), new UTF8Encoding(false));
            foreach (var line in edit(lines)) writer.Write(line + "\n");
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task TextChangedOnBothSidesKeepsBoth()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            db.Completions.Add(Done(C, H));
        });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.EditAsync(200, async db => (await db.Completions.SingleAsync(x => x.Id == C)).Notes = "from a");
        await b.EditAsync(300, async db => (await db.Completions.SingleAsync(x => x.Id == C)).Notes = "from b");

        var report = await b.MergeAsync(await a.WriteFileAsync());

        report.TextConflicts.Should().Be(1);
        var notes = (await b.QueryAsync(db => db.Completions.SingleAsync(x => x.Id == C))).Notes;
        notes.Should().StartWith("from b").And.Contain("from a");
    }
}
