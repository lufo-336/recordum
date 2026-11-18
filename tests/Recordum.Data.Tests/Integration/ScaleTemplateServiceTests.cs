using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ScaleTemplateServiceTests
{
    private static ScaleDraft Mood(string two = "Male") => new("Umore", 1, 5, ScalePrecision.Integer,
        [new(1, "Malissimo"), new(2, two), new(3, "Così così"), new(4, "Bene"), new(5, "Benissimo")]);

    private static async Task<Guid> HabitWithMetricAsync(DbFixture fx, Guid scaleId, decimal? recorded = null)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        var habit = new HabitNode { Id = Guid.NewGuid(), Name = "s" + Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State };
        var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habit.Id, Name = "M", Type = MetricType.Decimal, ScaleTemplateId = scaleId };
        db.HabitNodes.Add(habit);
        db.MetricDefinitions.Add(metric);
        if (recorded is { } v)
        {
            var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = habit.Id, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 };
            db.Completions.Add(c);
            db.CompletionValues.Add(new CompletionValue { Id = Guid.NewGuid(), CompletionId = c.Id, MetricDefinitionId = metric.Id, ValueDecimal = v });
        }
        await db.SaveChangesAsync();
        return metric.Id;
    }

    [Fact]
    public async Task CreateAsync_makes_the_anchors_and_their_derived_tags()
    {
        await using var fx = new DbFixture();
        var id = await new ScaleTemplateService(fx.Factory, new RecordingEventBus()).CreateAsync(Mood());

        await using var db = await fx.Factory.CreateDbContextAsync();
        var anchors = await db.ScaleAnchors.Include(a => a.Tag).Where(a => a.ScaleTemplateId == id).OrderBy(a => a.Value).ToListAsync();
        anchors.Select(a => a.Tag!.Name).Should().Equal("malissimo", "male", "così-così", "bene", "benissimo");
        (await db.ScaleTemplates.SingleAsync(s => s.Id == id)).IsOfficial.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_rename_creates_the_new_tag_and_keeps_the_old_one()
    {
        await using var fx = new DbFixture();
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var id = await svc.CreateAsync(Mood());

        await svc.UpdateAsync(id, Mood("Giù"));

        await using var db = await fx.Factory.CreateDbContextAsync();
        var two = await db.ScaleAnchors.Include(a => a.Tag).SingleAsync(a => a.ScaleTemplateId == id && a.Value == 2);
        two.Label.Should().Be("Giù");
        two.Tag!.Name.Should().Be("giù");
        (await db.Tags.AnyAsync(t => t.Name == "male")).Should().BeTrue();
        (await db.ScaleAnchors.CountAsync(a => a.ScaleTemplateId == id)).Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_narrowing_below_a_recorded_value_throws_and_changes_nothing()
    {
        await using var fx = new DbFixture();
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var id = await svc.CreateAsync(new("Voto", 0, 100, ScalePrecision.Integer, []));
        await HabitWithMetricAsync(fx, id, 80m);

        var act = async () => await svc.UpdateAsync(id, new("Voto", 0, 50, ScalePrecision.Integer, []));

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*narrow-max*");
        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.ScaleTemplates.SingleAsync(s => s.Id == id)).MaxValue.Should().Be(100);
    }

    [Fact]
    public async Task UpdateAsync_decimal_to_integer_with_a_recorded_1_37_throws_precision()
    {
        await using var fx = new DbFixture();
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var id = await svc.CreateAsync(new("Voto", 0, 10, ScalePrecision.Decimal, []));
        await HabitWithMetricAsync(fx, id, 1.37m);

        var act = async () => await svc.UpdateAsync(id, new("Voto", 0, 10, ScalePrecision.Integer, []));

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*precision*");
    }

    [Fact]
    public async Task UpdateAsync_narrowing_a_scale_without_recordings_is_fine()
    {
        await using var fx = new DbFixture();
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var id = await svc.CreateAsync(new("Voto", 0, 100, ScalePrecision.Integer, []));
        await HabitWithMetricAsync(fx, id);

        await svc.UpdateAsync(id, new("Voto", 0, 10, ScalePrecision.Integer, []));

        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.ScaleTemplates.SingleAsync(s => s.Id == id)).MaxValue.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_of_an_official_scale_throws()
    {
        await using var fx = new DbFixture();
        Guid id;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            id = (await db.ScaleTemplates.FirstAsync(s => s.IsOfficial)).Id;

        var act = async () => await new ScaleTemplateService(fx.Factory, new RecordingEventBus()).UpdateAsync(id, Mood());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DuplicateAsync_of_an_official_repoints_only_the_given_metric()
    {
        await using var fx = new DbFixture();
        Guid officialId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            officialId = (await db.ScaleTemplates.SingleAsync(s => s.Code == "mood-5")).Id;
        var first = await HabitWithMetricAsync(fx, officialId);
        var second = await HabitWithMetricAsync(fx, officialId);
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());

        var copyId = await svc.DuplicateAsync(officialId, "Il mio umore", [new(1, "Pessimo"), new(5, "Ottimo")], first);

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.MetricDefinitions.SingleAsync(m => m.Id == first)).ScaleTemplateId.Should().Be(copyId);
        (await db2.MetricDefinitions.SingleAsync(m => m.Id == second)).ScaleTemplateId.Should().Be(officialId);
        var copy = await db2.ScaleTemplates.Include(s => s.Anchors).SingleAsync(s => s.Id == copyId);
        copy.IsOfficial.Should().BeFalse();
        copy.Anchors.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUsageAsync_counts_habits_sharing_a_scale()
    {
        await using var fx = new DbFixture();
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var id = await svc.CreateAsync(Mood());
        await HabitWithMetricAsync(fx, id, 2.5m);
        await HabitWithMetricAsync(fx, id, 4m);

        var usage = await svc.GetUsageAsync(id);

        usage.Should().Be(new ScaleUsage(2, 2.5m, 4m, true));
    }

    [Fact]
    public async Task UpdateAsync_announces_every_habit_that_reads_the_scale()
    {
        await using var fx = new DbFixture();
        var bus = new RecordingEventBus();
        var svc = new ScaleTemplateService(fx.Factory, bus);
        var id = await svc.CreateAsync(Mood());
        var m1 = await HabitWithMetricAsync(fx, id);
        var m2 = await HabitWithMetricAsync(fx, id);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var habits = await db.MetricDefinitions.Where(m => m.Id == m1 || m.Id == m2).Select(m => m.HabitNodeId).ToListAsync();

        await svc.UpdateAsync(id, Mood("Meh"));

        bus.Published.OfType<Recordum.Plugins.Contracts.Events.HabitUpdatedPayload>().Select(p => p.HabitNodeId)
            .Should().BeEquivalentTo(habits);
    }

    // GIO-23 review finale: "Usata da N abitudini" non conta quelle nel cestino.
    [Fact]
    public async Task GetUsageAsync_does_not_count_habits_in_the_trash()
    {
        await using var fx = new DbFixture();
        var svc = new ScaleTemplateService(fx.Factory, new RecordingEventBus());
        var id = await svc.CreateAsync(new("Voto", 0, 10, ScalePrecision.Integer, []));
        await HabitWithMetricAsync(fx, id);
        var trashed = await HabitWithMetricAsync(fx, id);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var habitId = await db.MetricDefinitions.Where(m => m.Id == trashed).Select(m => m.HabitNodeId).SingleAsync();
            (await db.HabitNodes.SingleAsync(h => h.Id == habitId)).DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        (await svc.GetUsageAsync(id)).HabitCount.Should().Be(1);
    }
}
