using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures.Sync;
using static Recordum.Data.Tests.Sync.SyncMergerTests;

namespace Recordum.Data.Tests.Sync;

/// <summary>Spec §8.3: unificazione su chiave naturale (<c>tag.name</c>, <c>unit_of_measure.code</c>,
/// <c>scale_template.code</c>, <c>scale_anchor.(scale_template_id, value)</c>), riscrittura delle FK e alias nelle lapidi.</summary>
public sealed class NaturalKeyTests
{
    private static readonly Guid H1 = new("33333333-0000-0000-0000-000000000001");
    private static readonly Guid H2 = new("33333333-0000-0000-0000-000000000002");
    private static readonly Guid N1 = new("33333333-0000-0000-0000-000000000003");
    private static readonly Guid N2 = new("33333333-0000-0000-0000-000000000004");
    private static readonly Guid TagMin = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TagMax = new("90000000-0000-0000-0000-000000000009");
    private static readonly Guid UnitMin = new("10000000-0000-0000-0000-000000000011");
    private static readonly Guid UnitMax = new("90000000-0000-0000-0000-000000000019");
    private static readonly Guid ScaleId = new("33333333-0000-0000-0000-000000000020");

    [Fact]
    public async Task SameTagNameOnTwoDevicesBecomesOne()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H1, "Run"));
            db.Tags.Add(new Tag { Id = TagMax, Name = "Outdoor", Color = "#111111" });
            db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = H1, TagId = TagMax });
            db.Notes.Add(new Note { Id = N1, HabitNodeId = H1, BodyMd = "from a" });
            db.NoteTags.Add(new NoteTag { NoteId = N1, TagId = TagMax, Source = NoteTagSource.Property });
        });

        await b.EditAsync(200, db =>
        {
            db.HabitNodes.Add(Habit(H2, "Walk"));
            db.Tags.Add(new Tag { Id = TagMin, Name = "outdoor", Color = "#222222" });
            db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = H2, TagId = TagMin });
            db.Notes.Add(new Note { Id = N2, HabitNodeId = H2, BodyMd = "from b" });
            db.NoteTags.Add(new NoteTag { NoteId = N2, TagId = TagMin, Source = NoteTagSource.Inline });
        });

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        var reportB = await b.MergeAsync(fa);
        var reportA = await a.MergeAsync(fb);

        reportB.Repaired.Should().Be(1);
        reportA.Repaired.Should().Be(1);

        foreach (var d in new[] { a, b })
        {
            var tags = await d.QueryAsync(db => db.Tags.AsNoTracking().ToListAsync());
            tags.Should().ContainSingle().Which.Id.Should().Be(TagMin);

            var habitTags = await d.QueryAsync(db => db.HabitNodeTags.AsNoTracking().ToListAsync());
            habitTags.Should().HaveCount(2).And.OnlyContain(ht => ht.TagId == TagMin);

            var noteTags = await d.QueryAsync(db => db.NoteTags.AsNoTracking().ToListAsync());
            noteTags.Should().HaveCount(2).And.OnlyContain(nt => nt.TagId == TagMin);

            var tombs = await d.QueryAsync(db => db.SyncTombstones.AsNoTracking().Where(t => t.Entity == "tag").ToListAsync());
            tombs.Should().ContainSingle(t =>
                t.EntityId == TagMax.ToString("D").ToUpperInvariant()
                && t.AliasOf == TagMin.ToString("D").ToUpperInvariant());
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task AliasAppliedToLateRows()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H1, "Run"));
            db.Tags.Add(new Tag { Id = TagMin, Name = "focus" });
        });

        await b.EditAsync(200, db =>
        {
            db.HabitNodes.Add(Habit(H1, "Run"));
            db.Tags.Add(new Tag { Id = TagMax, Name = "Focus" });
        });

        // A unisce B: su A resta TagMin e TagMax diventa lapide con alias_of = TagMin.
        await a.MergeAsync(await b.WriteFileAsync());

        // B non ha ancora unito A: crea una nota tardiva legata a TagMax (il GUID scartato su A).
        var lateNote = new Guid("33333333-0000-0000-0000-000000000099");
        await b.EditAsync(300, db =>
        {
            db.Notes.Add(new Note { Id = lateNote, HabitNodeId = H1, BodyMd = "late note on discarded tag" });
            db.NoteTags.Add(new NoteTag { NoteId = lateNote, TagId = TagMax, Source = NoteTagSource.Property });
            db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = H1, TagId = TagMax });
        });

        var report = await a.MergeAsync(await b.WriteFileAsync());
        report.Repaired.Should().Be(0, "the alias tombstone already rewrites incoming rows before unification");

        var noteTagOnA = await a.QueryAsync(db => db.NoteTags.AsNoTracking().SingleAsync(nt => nt.NoteId == lateNote));
        noteTagOnA.TagId.Should().Be(TagMin);

        var habitTagOnA = await a.QueryAsync(db => db.HabitNodeTags.AsNoTracking().SingleAsync(ht => ht.HabitNodeId == H1));
        habitTagOnA.TagId.Should().Be(TagMin);

        await b.MergeAsync(await a.WriteFileAsync());
        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task SameScaleEditedOnBothDevicesConverges()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        var initial1 = new Guid("30000000-0000-0000-0000-000000000001");
        var initial2 = new Guid("30000000-0000-0000-0000-000000000002");
        await a.EditAsync(100, db =>
        {
            db.ScaleTemplates.Add(new ScaleTemplate
            {
                Id = ScaleId, Code = "energy-custom", Name = "Energy", MinValue = 1, MaxValue = 2,
            });
            db.ScaleAnchors.AddRange(
                new ScaleAnchor { Id = initial1, ScaleTemplateId = ScaleId, Value = 1, Label = "Low" },
                new ScaleAnchor { Id = initial2, ScaleTemplateId = ScaleId, Value = 2, Label = "High" });
        });
        await b.MergeAsync(await a.WriteFileAsync());

        // Sia A che B ricostruiscono gli scaglioni della scala con GUID nuovi.
        var a1 = new Guid("11000000-0000-0000-0000-000000000001");
        var a2 = new Guid("88000000-0000-0000-0000-000000000002");
        var b1 = new Guid("77000000-0000-0000-0000-000000000001");
        var b2 = new Guid("22000000-0000-0000-0000-000000000002");

        await a.EditAsync(200, async db =>
        {
            await db.ScaleAnchors.Where(x => x.ScaleTemplateId == ScaleId).ExecuteDeleteAsync();
            db.ScaleAnchors.AddRange(
                new ScaleAnchor { Id = a1, ScaleTemplateId = ScaleId, Value = 1, Label = "Low A" },
                new ScaleAnchor { Id = a2, ScaleTemplateId = ScaleId, Value = 2, Label = "High A" });
        });

        await b.EditAsync(300, async db =>
        {
            await db.ScaleAnchors.Where(x => x.ScaleTemplateId == ScaleId).ExecuteDeleteAsync();
            db.ScaleAnchors.AddRange(
                new ScaleAnchor { Id = b1, ScaleTemplateId = ScaleId, Value = 1, Label = "Low B" },
                new ScaleAnchor { Id = b2, ScaleTemplateId = ScaleId, Value = 2, Label = "High B" });
        });

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        var reportB = await b.MergeAsync(fa);
        var reportA = await a.MergeAsync(fb);

        reportB.Repaired.Should().Be(2);
        reportA.Repaired.Should().Be(2);

        foreach (var d in new[] { a, b })
        {
            var anchors = await d.QueryAsync(db => db.ScaleAnchors.AsNoTracking()
                .Where(x => x.ScaleTemplateId == ScaleId)
                .OrderBy(x => x.Value)
                .ToListAsync());

            anchors.Should().HaveCount(2);
            anchors[0].Id.Should().Be(a1, "a1 < b1 for value 1");
            anchors[0].Label.Should().Be("Low B", "B's edit is newer");
            anchors[1].Id.Should().Be(b2, "b2 < a2 for value 2");
            anchors[1].Label.Should().Be("High B", "B's edit is newer");
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task UnitCodeCollisionUnified()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        var metricA = new Guid("33333333-0000-0000-0000-000000000031");
        var metricB = new Guid("33333333-0000-0000-0000-000000000032");

        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H1, "Drink"));
            db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = UnitMax, Code = "cup_custom", Name = "Cup", Symbol = "cp" });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricA, HabitNodeId = H1, Name = "Amount A", Type = MetricType.Integer, UnitOfMeasureId = UnitMax,
            });
        });

        await b.EditAsync(200, db =>
        {
            db.HabitNodes.Add(Habit(H2, "Tea"));
            db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = UnitMin, Code = "cup_custom", Name = "Teacup", Symbol = "tc" });
            db.MetricDefinitions.Add(new MetricDefinition
            {
                Id = metricB, HabitNodeId = H2, Name = "Amount B", Type = MetricType.Integer, UnitOfMeasureId = UnitMin,
            });
        });

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        var reportB = await b.MergeAsync(fa);
        var reportA = await a.MergeAsync(fb);

        reportB.Repaired.Should().Be(1);
        reportA.Repaired.Should().Be(1);

        foreach (var d in new[] { a, b })
        {
            var units = await d.QueryAsync(db => db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.Code == "cup_custom").ToListAsync());
            units.Should().ContainSingle().Which.Id.Should().Be(UnitMin);

            var metrics = await d.QueryAsync(db => db.MetricDefinitions.AsNoTracking().ToListAsync());
            metrics.Should().HaveCount(2).And.OnlyContain(m => m.UnitOfMeasureId == UnitMin);

            var tombs = await d.QueryAsync(db => db.SyncTombstones.AsNoTracking()
                .Where(t => t.Entity == "unit_of_measure").ToListAsync());
            tombs.Should().ContainSingle(t =>
                t.EntityId == UnitMax.ToString("D").ToUpperInvariant()
                && t.AliasOf == UnitMin.ToString("D").ToUpperInvariant());
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    private static async Task MeshAsync(params SyncDevice[] ds)
    {
        var files = new List<(SyncDevice, byte[])>();
        foreach (var d in ds) files.Add((d, await d.WriteFileAsync()));
        foreach (var d in ds)
            foreach (var (src, f) in files)
                if (src != d) await d.MergeAsync(f);
    }

    private static async Task AssertConvergedAsync(params SyncDevice[] ds)
    {
        var dump = await ds[0].CanonicalDumpAsync();
        foreach (var d in ds.Skip(1)) (await d.CanonicalDumpAsync()).Should().Be(dump, $"{d.Name} vs {ds[0].Name}");
        await MeshAsync(ds);
        await MeshAsync(ds);
        (await ds[0].CanonicalDumpAsync()).Should().Be(dump, "fixed point");
    }

    [Fact]
    public async Task AliasToATargetDeletedElsewhereConverges()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        await a.EditAsync(100, db =>
        {
            db.HabitNodes.Add(Habit(H1, "Run"));
            db.Tags.Add(new Tag { Id = TagMax, Name = "Outdoor" });
            db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = H1, TagId = TagMax });
        });

        await b.EditAsync(200, db => db.Tags.Add(new Tag { Id = TagMin, Name = "outdoor" }));
        await b.MergeAsync(await a.WriteFileAsync());

        // B cancella il tag vincitore dopo l'unificazione; A riceve sia l'alias TagMax -> TagMin che la cancellazione di TagMin.
        await b.EditAsync(300, db => db.Tags.Where(t => t.Id == TagMin).ExecuteDeleteAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        await MeshAsync(a, b);
        await MeshAsync(a, b);

        var tagMaxId = TagMax.ToString("D").ToUpperInvariant();
        var tagMinId = TagMin.ToString("D").ToUpperInvariant();
        foreach (var d in new[] { a, b })
        {
            (await d.QueryAsync(db => db.Tags.AsNoTracking().ToListAsync())).Should().BeEmpty();
            (await d.QueryAsync(db => db.HabitNodeTags.AsNoTracking().Where(ht => ht.HabitNodeId == H1).ToListAsync()))
                .Should().BeEmpty();

            var tombs = await d.QueryAsync(db => db.SyncTombstones.AsNoTracking()
                .Where(t => t.Entity == "tag").ToListAsync());
            tombs.Should().ContainSingle(t => t.EntityId == tagMaxId && t.AliasOf == tagMinId);
            tombs.Should().ContainSingle(t => t.EntityId == tagMinId && t.AliasOf == null);
        }

        await AssertConvergedAsync(a, b);
    }

    [Fact]
    public async Task DeletionAndAliasOfTheSameIdConvergeWithThreeDevices()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");

        await a.EditAsync(100, db => db.Tags.Add(new Tag { Id = TagMax, Name = "Outdoor" }));
        await c.MergeAsync(await a.WriteFileAsync());

        await b.EditAsync(200, db => db.Tags.Add(new Tag { Id = TagMin, Name = "outdoor" }));
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);

        // C non conosce ancora B e cancella TagMax concorrentemente a t=300.
        await c.EditAsync(300, db => db.Tags.Where(t => t.Id == TagMax).ExecuteDeleteAsync());

        await a.MergeAsync(await c.WriteFileAsync());
        await a.MergeAsync(fb);

        for (var i = 0; i < 3; i++)
            await MeshAsync(a, b, c);

        var tagMaxId = TagMax.ToString("D").ToUpperInvariant();
        var tagMinId = TagMin.ToString("D").ToUpperInvariant();
        var tombs = new List<Models.SyncTombstone>();
        foreach (var d in new[] { a, b, c })
        {
            (await d.QueryAsync(db => db.Tags.AsNoTracking().ToListAsync()))
                .Should().ContainSingle().Which.Id.Should().Be(TagMin);
            var tomb = await d.QueryAsync(db => db.SyncTombstones.AsNoTracking()
                .SingleAsync(t => t.Entity == "tag" && t.EntityId == tagMaxId));
            tomb.DeletedAtMs.Should().Be(300);
            tomb.AliasOf.Should().Be(tagMinId);
            tombs.Add(tomb);
        }

        tombs[1].Should().BeEquivalentTo(tombs[0]);
        tombs[2].Should().BeEquivalentTo(tombs[0]);

        await AssertConvergedAsync(a, b, c);
    }

    [Fact]
    public async Task UnifiedRowsMergeFieldByField()
    {
        static async Task<(ScaleAnchor Anchor, Dictionary<string, long> FieldMs)> RunAsync(bool viaThirdDevice)
        {
            var tag1 = new Guid("33333333-0000-0000-0000-000000000021");
            var anchorMin = new Guid("10000000-0000-0000-0000-000000000021");
            var anchorMax = new Guid("90000000-0000-0000-0000-000000000029");
            var anchorMinId = anchorMin.ToString("D").ToUpperInvariant();
            var anchorMaxId = anchorMax.ToString("D").ToUpperInvariant();

            await using var a = new SyncDevice("a");
            await using var b = new SyncDevice("b");
            await using var c = new SyncDevice("c");

            await a.EditAsync(50, db =>
            {
                db.ScaleTemplates.Add(new ScaleTemplate
                {
                    Id = ScaleId, Code = "effort", Name = "Effort", MinValue = 1, MaxValue = 5,
                });
                db.Tags.Add(new Tag { Id = tag1, Name = "Easy" });
            });
            var fShared = await a.WriteFileAsync();
            await b.MergeAsync(fShared);
            await c.MergeAsync(fShared);

            await a.EditAsync(100, db => db.ScaleAnchors.Add(new ScaleAnchor
            {
                Id = anchorMax, ScaleTemplateId = ScaleId, Value = 1, Label = "Easy", TagId = null,
            }));
            var fa100 = await a.WriteFileAsync();

            await b.EditAsync(200, db => db.ScaleAnchors.Add(new ScaleAnchor
            {
                Id = anchorMin, ScaleTemplateId = ScaleId, Value = 1, Label = "Easy", TagId = tag1,
            }));

            if (viaThirdDevice)
            {
                await c.MergeAsync(fa100);
                await c.MergeAsync(await b.WriteFileAsync());
                await a.EditAsync(300, async db =>
                    (await db.ScaleAnchors.SingleAsync(x => x.Id == anchorMax)).Label = "Easy A");
                await c.MergeAsync(await a.WriteFileAsync());
                var fc = await c.WriteFileAsync();
                await b.MergeAsync(fc);
                await a.MergeAsync(fc);
                for (var i = 0; i < 3; i++)
                    await MeshAsync(a, b, c);
                await AssertConvergedAsync(a, b, c);
            }
            else
            {
                await a.EditAsync(300, async db =>
                    (await db.ScaleAnchors.SingleAsync(x => x.Id == anchorMax)).Label = "Easy A");
                for (var i = 0; i < 3; i++)
                    await MeshAsync(a, b);
                await AssertConvergedAsync(a, b);
            }

            var anchor = await a.QueryAsync(db => db.ScaleAnchors.AsNoTracking().SingleAsync(x => x.ScaleTemplateId == ScaleId));
            anchor.Id.Should().Be(anchorMin);
            anchor.Label.Should().Be("Easy A");
            anchor.TagId.Should().Be(tag1);
            anchor.UpdatedAtMs.Should().Be(300);

            var fieldMs = await a.QueryAsync(db => db.SyncFieldMs.AsNoTracking()
                .Where(f => f.Entity == "scale_anchor" && f.EntityId == anchorMinId)
                .ToDictionaryAsync(f => f.Field, f => f.Ms));
            fieldMs.Should().ContainKey("tag_id").WhoseValue.Should().Be(200);
            fieldMs.Should().ContainKey("created_at").WhoseValue.Should().Be(200);
            fieldMs.Should().NotContainKey("label");

            var tomb = await a.QueryAsync(db => db.SyncTombstones.AsNoTracking()
                .SingleAsync(t => t.Entity == "scale_anchor" && t.EntityId == anchorMaxId));
            tomb.AliasOf.Should().Be(anchorMinId);
            return (anchor, fieldMs);
        }

        var direct = await RunAsync(viaThirdDevice: false);
        var viaC = await RunAsync(viaThirdDevice: true);

        viaC.Anchor.Should().BeEquivalentTo(direct.Anchor);
        viaC.FieldMs.Should().BeEquivalentTo(direct.FieldMs);
    }

    [Fact]
    public async Task RetaggedHabitTagConvergesAcrossRenames()
    {
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");

        await c.EditAsync(50, db => db.HabitNodes.Add(Habit(H1, "Run")));
        await b.MergeAsync(await c.WriteFileAsync());

        await c.EditAsync(100, db =>
        {
            db.Tags.Add(new Tag { Id = TagMin, Name = "outdoor" });
            db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = H1, TagId = TagMin });
        });

        await b.EditAsync(200, db =>
        {
            db.Tags.Add(new Tag { Id = TagMax, Name = "sport" });
            db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = H1, TagId = TagMax });
        });

        await c.MergeAsync(await b.WriteFileAsync());

        await b.EditAsync(300, async db => (await db.Tags.SingleAsync(t => t.Id == TagMax)).Name = "outdoor");

        for (var i = 0; i < 3; i++)
            await MeshAsync(b, c);

        var htOnB = await b.QueryAsync(db => db.HabitNodeTags.AsNoTracking().ToListAsync());
        var htOnC = await c.QueryAsync(db => db.HabitNodeTags.AsNoTracking().ToListAsync());
        htOnB.Should().ContainSingle();
        htOnC.Should().BeEquivalentTo(htOnB);

        var fmsOnB = await b.QueryAsync(db => db.SyncFieldMs.AsNoTracking()
            .Where(f => f.Entity == "habit_node_tag").OrderBy(f => f.EntityId).ThenBy(f => f.Field).ToListAsync());
        var fmsOnC = await c.QueryAsync(db => db.SyncFieldMs.AsNoTracking()
            .Where(f => f.Entity == "habit_node_tag").OrderBy(f => f.EntityId).ThenBy(f => f.Field).ToListAsync());
        fmsOnC.Should().BeEquivalentTo(fmsOnB);

        await AssertConvergedAsync(b, c);
    }

    [Fact]
    public async Task AliasedRowOfTheSameFileIsNotTakenForStale()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");

        await a.EditAsync(100, db => db.Tags.Add(new Tag { Id = TagMin, Name = "outdoor", Color = "#000000" }));
        await b.EditAsync(100, db => db.Tags.Add(new Tag { Id = TagMax, Name = "temp", Color = "#111111" }));
        await c.MergeAsync(await a.WriteFileAsync());
        await c.MergeAsync(await b.WriteFileAsync());

        // A e B unificano TagMax -> TagMin prima che C riceva l'alias.
        await b.EditAsync(120, async db => (await db.Tags.SingleAsync(t => t.Id == TagMax)).Name = "Outdoor");
        await a.MergeAsync(await b.WriteFileAsync());

        // C ha ancora tutte e due le righe: aggiorna il colore di TagMax a t=150 e poi il nome di TagMin a t=200.
        await c.EditAsync(150, async db => (await db.Tags.SingleAsync(t => t.Id == TagMax)).Color = "#FF0000");
        await c.EditAsync(200, async db => (await db.Tags.SingleAsync(t => t.Id == TagMin)).Name = "outdoor-c");

        await a.MergeAsync(await c.WriteFileAsync());

        var tagOnA = await a.QueryAsync(db => db.Tags.AsNoTracking().SingleAsync());
        tagOnA.Id.Should().Be(TagMin);
        tagOnA.Name.Should().Be("outdoor-c");
        tagOnA.Color.Should().Be("#FF0000", "TagMax's newer color must not be skipped as stale after TagMin was merged");

        for (var i = 0; i < 3; i++)
            await MeshAsync(a, b, c);
        await AssertConvergedAsync(a, b, c);
    }
}
