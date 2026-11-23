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
}
