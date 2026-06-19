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

public class DiaryServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedJournalAsync(FakeClock? clock = null)
    {
        var fx = new DbFixture(clock: clock);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    private static DiaryService NewService(DbFixture fx, out RecordingEventBus bus)
    {
        bus = new RecordingEventBus();
        return new DiaryService(fx.Factory, fx.Clock, NullLogger<DiaryService>.Instance, bus);
    }

    [Fact]
    public async Task SaveEntryAsync_creates_paired_note_and_completion_and_publishes_events()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out var bus);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);

        var noteId = await sut.SaveEntryAsync(habitId, today, "dear diary");

        await using var db = await fx.Factory.CreateDbContextAsync();
        var note = await db.Notes.SingleAsync(n => n.Id == noteId);
        note.Category.Should().Be(NoteCategory.Diario);
        note.TargetDate.Should().Be(today);
        note.BodyMd.Should().Be("dear diary");
        note.CompletionId.Should().NotBeNull();

        var completion = await db.Completions.SingleAsync(c => c.Id == note.CompletionId!.Value);
        completion.HabitNodeId.Should().Be(habitId);
        DateOnly.FromDateTime(completion.CompletedAt).Should().Be(today);
        completion.IsRetroactive.Should().BeFalse();

        // version history snapshot exists
        (await db.NoteHistories.CountAsync(h => h.NoteId == noteId)).Should().Be(1);

        bus.Published.Should().Contain(p => p is NoteCreatedPayload);
        bus.Published.Should().Contain(p => p is CompletionRecordedPayload);

        await fx.DisposeAsync();
    }

    [Fact]
    public async Task SaveEntryAsync_past_day_marks_completion_retroactive_on_that_day()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var pastDay = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime).AddDays(-3);

        var noteId = await sut.SaveEntryAsync(habitId, pastDay, "backfilled");

        await using var db = await fx.Factory.CreateDbContextAsync();
        var note = await db.Notes.SingleAsync(n => n.Id == noteId);
        var completion = await db.Completions.SingleAsync(c => c.Id == note.CompletionId!.Value);
        DateOnly.FromDateTime(completion.CompletedAt).Should().Be(pastDay);
        completion.IsRetroactive.Should().BeTrue();
        note.TargetDate.Should().Be(pastDay);

        await fx.DisposeAsync();
    }
}
