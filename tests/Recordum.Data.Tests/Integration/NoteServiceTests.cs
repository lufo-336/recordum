using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class NoteServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedHabitAsync(FakeClock? clock = null)
    {
        var fx = new DbFixture(clock: clock);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "h" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    private static NoteService NewService(DbFixture fx, out RecordingEventBus bus)
    {
        bus = new RecordingEventBus();
        return new NoteService(fx.Factory, fx.Clock, NullLogger<NoteService>.Instance, bus);
    }

    [Fact]
    public async Task CreateAsync_persists_row_and_publishes_NoteCreated()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out var bus);

        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "hello" });

        id.Should().NotBe(Guid.Empty);
        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<NoteCreatedPayload>()
            .Which.NoteId.Should().Be(id);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var persisted = await db.Notes.Where(n => n.Id == id).SingleAsync();
        persisted.BodyMd.Should().Be("hello");
        persisted.HabitNodeId.Should().Be(habitId);

        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_mutates_body_and_publishes_NoteUpdated()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out var bus);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "orig" });
        bus.Published.Clear();

        await sut.UpdateAsync(id, n => n.BodyMd = "new");

        bus.Published.Should().ContainSingle().Which.Should().BeOfType<NoteUpdatedPayload>();
        (await sut.GetAsync(id))!.BodyMd.Should().Be("new");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_no_publish_when_id_missing()
    {
        var (fx, _) = await SeedHabitAsync();
        var sut = NewService(fx, out var bus);

        await sut.UpdateAsync(Guid.NewGuid(), n => n.BodyMd = "x");

        bus.Published.Should().BeEmpty();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_removes_row_and_publishes_NoteDeleted()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out var bus);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "x" });
        bus.Published.Clear();

        await sut.DeleteAsync(id);

        bus.Published.Should().ContainSingle().Which.Should().BeOfType<NoteDeletedPayload>();
        (await sut.GetAsync(id)).Should().BeNull();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_no_publish_when_id_missing()
    {
        var (fx, _) = await SeedHabitAsync();
        var sut = NewService(fx, out var bus);

        await sut.DeleteAsync(Guid.NewGuid());

        bus.Published.Should().BeEmpty();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListByHabitAsync_returns_notes_ordered_by_updated_desc()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var (fx, habitId) = await SeedHabitAsync(clock);
        var sut = NewService(fx, out _);

        var a = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "a" });
        clock.Advance(TimeSpan.FromMinutes(2));
        var b = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "b" });
        clock.Advance(TimeSpan.FromMinutes(-1)); // c is between a and b
        var c = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "c" });

        var list = await sut.ListByHabitAsync(habitId);

        list.Select(n => n.Id).Should().Equal(b, c, a);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListByHabitAsync_filters_to_habit_scope()
    {
        await using var fx = new DbFixture();
        var h1 = Guid.NewGuid();
        var h2 = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = h1, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "h1" });
            db.HabitNodes.Add(new HabitNode { Id = h2, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "h2" });
            await db.SaveChangesAsync();
        }
        var sut = NewService(fx, out _);
        await sut.CreateAsync(new Note { HabitNodeId = h1, BodyMd = "a" });
        await sut.CreateAsync(new Note { HabitNodeId = h2, BodyMd = "b" });

        var list = await sut.ListByHabitAsync(h1);

        list.Should().ContainSingle().Which.HabitNodeId.Should().Be(h1);
    }

    [Fact]
    public async Task CreateAsync_with_attachments_persists_both()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out _);

        var id = await sut.CreateAsync(
            new Note { HabitNodeId = habitId, BodyMd = "hi" },
            new[] { new Attachment { Id = Guid.NewGuid(), FilePath = "/tmp/a.png", MimeType = "image/png" } });

        await using var db = await fx.Factory.CreateDbContextAsync();
        var note = await db.Notes.Include(n => n.Attachments).SingleAsync();
        note.Id.Should().Be(id);
        note.Attachments.Should().ContainSingle();
        note.UpdatedAtMs.Should().BeGreaterThan(0);

        await fx.DisposeAsync();
    }
}
