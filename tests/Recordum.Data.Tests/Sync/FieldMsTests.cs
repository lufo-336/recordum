using System.IO.Compression;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Models;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Recordum.Data.Tests.Fixtures.Sync;
using static Recordum.Data.Tests.Sync.SyncMergerTests;

namespace Recordum.Data.Tests.Sync;

/// <summary>Ruling 20: ogni campo ha la sua ora. <c>sync_field_ms</c> tiene solo i campi più vecchi della riga
/// (assente = l'ora della riga); i trigger la tengono, il formato la porta in <c>_field_ms</c>, l'unione decide campo per
/// campo con l'ora più recente (a parità, il valore con la forma canonica minore).</summary>
public sealed class FieldMsTests
{
    private static readonly Guid H = new("55555555-0000-0000-0000-000000000001");

    private static string Id(Guid id) => id.ToString("D").ToUpperInvariant();

    private static async Task<Dictionary<string, long>> FieldMsAsync(IDbContextFactory<RecordumDbContext> factory, string entity, Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.OpenConnectionAsync();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT field, ms FROM sync_field_ms WHERE entity = $e AND entity_id = $id";
        var pe = cmd.CreateParameter(); pe.ParameterName = "$e"; pe.Value = entity; cmd.Parameters.Add(pe);
        var pi = cmd.CreateParameter(); pi.ParameterName = "$id"; pi.Value = Id(id); cmd.Parameters.Add(pi);
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result[reader.GetString(0)] = reader.GetInt64(1);
        return result;
    }

    [Fact]
    public async Task UpdateRecordsTheOlderFieldsOnly()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(1_000));
        await using var fx = new DbFixture(clock);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(Habit(H, "Run"));
            await db.SaveChangesAsync();
        }

        (await FieldMsAsync(fx.Factory, "habit_node", H)).Should().BeEmpty("a new row has every field at the row's time");

        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(2_000));
        await using (var db = await fx.Factory.CreateDbContextAsync())
            await db.HabitNodes.Where(n => n.Id == H).ExecuteUpdateAsync(s => s.SetProperty(n => n.Name, "Walk"));

        var after1 = await FieldMsAsync(fx.Factory, "habit_node", H);
        after1.Should().ContainKey("color").WhoseValue.Should().Be(1_000);
        after1.Should().NotContainKey("name", "the changed field is at the row's new time");
        after1.Should().NotContainKey("updated_at_ms");
        after1.Values.Should().OnlyContain(ms => ms == 1_000);

        clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(3_000));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.HabitNodes.SingleAsync(n => n.Id == H)).Color = "#111111";
            await db.SaveChangesAsync();
        }

        var after2 = await FieldMsAsync(fx.Factory, "habit_node", H);
        after2.Should().NotContainKey("color");
        after2.Should().ContainKey("name").WhoseValue.Should().Be(2_000);
        after2.Should().ContainKey("icon").WhoseValue.Should().Be(1_000, "an untouched field keeps its first time");

        await using (var db = await fx.Factory.CreateDbContextAsync())
            await db.HabitNodes.IgnoreQueryFilters().Where(n => n.Id == H).ExecuteDeleteAsync();
        (await FieldMsAsync(fx.Factory, "habit_node", H)).Should().BeEmpty("a deleted row leaves no field times");
    }

    [Fact]
    public async Task FileCarriesFieldMsOfOlderFields()
    {
        await using var a = new SyncDevice("a");
        await a.EditAsync(1_000, db => db.HabitNodes.Add(Habit(H, "Run")));
        await a.EditAsync(2_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");

        using var zip = new MemoryStream(await a.WriteZipAsync());
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var lines = new StreamReader(archive.GetEntry("data/habit_node.jsonl")!.Open());
            var line = await lines.ReadToEndAsync();
            line.Should().Contain("\"_field_ms\":{").And.Contain("\"color\":1000");
        }

        zip.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zip);
        var row = snapshot.Rows["habit_node"].Single(r => r.Id == Id(H));
        row.FieldMs.Should().ContainKey("color").WhoseValue.Should().Be(1_000);
        row.FieldMs.Should().NotContainKey("name");
        row.Fields.Should().NotContainKey("_field_ms", "field times are not a field");
    }

    /// <summary>Prima unione fra A e B (nessuna base fra loro): ognuno ha cambiato un campo diverso della stessa riga,
    /// presa da C. Tutti e due i cambi restano (era il limite della regola della riga intera, spec §8.4).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstMergeKeepsBothFieldEdits(bool bFirst)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");
        await c.EditAsync(10_000, db => db.HabitNodes.Add(Habit(H, "Run")));
        var fc = await c.WriteFileAsync();
        await a.MergeAsync(fc);
        await b.MergeAsync(fc);
        await a.EditAsync(20_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");
        await b.EditAsync(30_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Color = "#111111");

        await TextConflictTests.RoundAsync(a, b, bFirst ? "b-first" : "a-first");

        foreach (var d in new[] { a, b })
        {
            var h = (await HabitAsync(d, H))!;
            h.Name.Should().Be("Walk", d.Name);
            h.Color.Should().Be("#111111", d.Name);
            h.UpdatedAtMs.Should().Be(30_000, "the row's time is its newest field's");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    /// <summary>Stessa ora sullo stesso campo, valori diversi: vince la forma canonica minore del valore, ovunque.</summary>
    [Theory]
    [InlineData("a-first")]
    [InlineData("b-first")]
    [InlineData("crossed")]
    public async Task SameFieldTimeBreaksTieByValue(string order)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(10_000, db => db.HabitNodes.Add(Habit(H, "Run")));
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());
        await a.EditAsync(20_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Y");
        await b.EditAsync(20_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "X");

        await TextConflictTests.RoundAsync(a, b, order);
        await TextConflictTests.RoundAsync(a, b, order);

        foreach (var d in new[] { a, b }) (await HabitAsync(d, H))!.Name.Should().Be("X", d.Name);
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    /// <summary>Il caso che ha fatto nascere il Ruling 20: A cambia, B rimette com'era, C fa da ponte con file vecchi.
    /// La modifica più recente (il ritorno di B) vince ovunque e le copie si fermano, senza che le ore salgano.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevertWinsThroughABridgeDevice(bool cFirst)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");
        await a.EditAsync(10_000, db => db.HabitNodes.Add(Habit(H, "Run")));
        var f0 = await a.WriteFileAsync();
        await b.MergeAsync(f0);
        await c.MergeAsync(f0);
        await a.MergeAsync(await b.WriteFileAsync());
        await a.MergeAsync(await c.WriteFileAsync());
        await b.MergeAsync(await c.WriteFileAsync());

        await a.EditAsync(20_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");
        var f1 = await a.WriteFileAsync();
        await a.MergeAsync(await c.WriteFileAsync());
        await c.MergeAsync(f1);
        await b.MergeAsync(f1);
        await b.EditAsync(40_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Run");

        for (var i = 0; i < 4; i++)
        {
            var fa = await a.WriteFileAsync();
            var fb = await b.WriteFileAsync();
            var fcc = await c.WriteFileAsync();
            if (cFirst) { await a.MergeAsync(fcc); await b.MergeAsync(fcc); }
            await a.MergeAsync(fb);
            await b.MergeAsync(fa);
            if (!cFirst) { await a.MergeAsync(fcc); await b.MergeAsync(fcc); }
            await c.MergeAsync(fa);
            await c.MergeAsync(fb);
        }

        foreach (var d in new[] { a, b, c })
        {
            var h = (await HabitAsync(d, H))!;
            h.Name.Should().Be("Run", d.Name);
            h.UpdatedAtMs.Should().Be(40_000, $"{d.Name}: the times stop at the newest edit");
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
        (await a.CanonicalDumpAsync()).Should().Be(await c.CanonicalDumpAsync());
    }

    private static readonly Guid N = new("55555555-0000-0000-0000-000000000002");
    private static readonly Guid At = new("55555555-0000-0000-0000-000000000003");

    private static Note QuickNote(string body) => new()
    {
        Id = N, HabitNodeId = H, Category = Recordum.Core.Enums.NoteCategory.Veloce,
        TargetDate = new DateOnly(2026, 11, 22), BodyMd = body,
    };

    private static async Task<string> BodyAsync(SyncDevice d) =>
        (await d.QueryAsync(db => db.Notes.AsNoTracking().SingleAsync(n => n.Id == N))).BodyMd;

    private static Task SetBodyAsync(SyncDevice d, long atMs, string body) =>
        d.EditAsync(atMs, async db => (await db.Notes.SingleAsync(n => n.Id == N)).BodyMd = body);

    /// <summary>Review 7b, punto 1: B toglie i blocchi di un testo unito mentre A scrive un paragrafo nuovo. La pulizia di
    /// B è più recente, ma il paragrafo di A non l'ha visto nessuno: resta, in coda.</summary>
    [Theory]
    [InlineData("a-first")]
    [InlineData("b-first")]
    [InlineData("crossed")]
    public async Task NewTextSurvivesACleanupElsewhere(string order)
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(1_000, db => { db.HabitNodes.Add(Habit(H, "Journal")); db.Notes.Add(QuickNote("W\n\n— a · 1970-01-01 00:00 —\nL")); });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());
        await SetBodyAsync(a, 2_000, "Paragrafo nuovo di A");
        await SetBodyAsync(b, 3_000, "W");

        await TextConflictTests.RoundAsync(a, b, order);
        await TextConflictTests.RoundAsync(a, b, order);

        foreach (var d in new[] { a, b })
        {
            var body = await BodyAsync(d);
            body.Should().StartWith("W", d.Name).And.Contain("Paragrafo nuovo di A", d.Name);
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    /// <summary>Review 7b, punto 1 (ABA): B scrive e poi rimette il testo di partenza, più tardi di A che ha scritto un
    /// testo nuovo. Il ritorno di B vince, il testo di A resta in coda.</summary>
    [Fact]
    public async Task NewTextSurvivesARevertElsewhere()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await a.EditAsync(1_000, db => { db.HabitNodes.Add(Habit(H, "Journal")); db.Notes.Add(QuickNote("base")); });
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());
        await SetBodyAsync(a, 2_000, "Appunti di A");
        await SetBodyAsync(b, 2_500, "bozza");
        await SetBodyAsync(b, 3_000, "base");

        await TextConflictTests.CrossAsync(a, b);
        await TextConflictTests.CrossAsync(a, b);

        foreach (var d in new[] { a, b })
        {
            var body = await BodyAsync(d);
            body.Should().StartWith("base", d.Name).And.Contain("Appunti di A", d.Name);
        }
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    /// <summary>Review 7b, punto 3: su B la foto cambia (presa da A) ma l'ora della riga no, perché un altro campo è più
    /// recente. C, che aveva già quella riga di B, deve comunque prendere la foto nuova.</summary>
    [Fact]
    public async Task PhotoChangedWithoutARowTimeChangeArrives()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");
        var photo1 = SyncSeed.CreateSamplePhotoBytes();
        var photo2 = photo1.Reverse().ToArray();
        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H, "Journal"));
            db.Notes.Add(QuickNote("photo"));
            db.Attachments.Add(new Recordum.Data.Models.Attachment
            {
                Id = At, NoteId = N, Filename = "a.jpg", MimeType = "image/jpeg",
                FileSizeBytes = photo1.Length, Content = photo1, Thumbnail = SyncSeed.CreateSampleThumbnailBytes(),
            });
        });
        var f0 = await a.WriteFileAsync();
        await b.MergeAsync(f0);
        await c.MergeAsync(f0);

        await a.EditAsync(150, async db => (await db.Attachments.SingleAsync()).Content = photo2);
        await b.EditAsync(200, async db => (await db.Attachments.SingleAsync()).Filename = "b.jpg");
        await c.MergeAsync(await b.WriteFileAsync());
        await b.MergeAsync(await a.WriteFileAsync());
        await c.MergeAsync(await b.WriteFileAsync());

        foreach (var d in new[] { b, c })
        {
            var att = await d.QueryAsync(db => db.Attachments.AsNoTracking().SingleAsync());
            att.Content.Should().Equal(photo2, d.Name);
            att.Filename.Should().Be("b.jpg", d.Name);
        }
        (await b.CanonicalDumpAsync()).Should().Be(await c.CanonicalDumpAsync());
    }

    /// <summary>Review 7b, punto 4: una modifica fatta con l'orologio indietro rispetto alla riga (arrivata da un pari
    /// con l'orologio avanti) la batte lo stesso: l'ora diventa la vecchia + 1, e le ore dei campi restano sotto.</summary>
    [Fact]
    public async Task AnEditBeatsTheVersionItEditsEvenWithAClockBehind()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await b.EditAsync(5_000, db => db.HabitNodes.Add(Habit(H, "Run")));
        await a.MergeAsync(await b.WriteFileAsync());
        await a.EditAsync(4_000, async db => (await db.HabitNodes.SingleAsync(n => n.Id == H)).Name = "Walk");

        var onA = (await HabitAsync(a, H))!;
        onA.UpdatedAtMs.Should().Be(5_001);
        (await FieldMsAsync(a.Fixture.Factory, "habit_node", H)).Values.Should().OnlyContain(ms => ms < 5_001);

        await b.MergeAsync(await a.WriteFileAsync());
        (await HabitAsync(b, H))!.Name.Should().Be("Walk");
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }
}
