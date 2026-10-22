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

    private static Recordum.Core.Abstractions.Services.IDiaryService NewService(DbFixture fx, out RecordingEventBus bus)
    {
        bus = new RecordingEventBus();
        return new DiaryService(fx.Factory, fx.Clock, NullLogger<DiaryService>.Instance, bus, fx.ResetTimes);
    }

    /// <summary>A11/B16. Una voce di diario datata a un giorno passato si stampiglia a mezzogiorno
    /// locale di quel giorno: mezzanotte UTC, col confine locale, cadeva nel giorno prima — e la voce
    /// spariva dal giorno che l'utente aveva scelto.</summary>
    [Fact]
    public async Task A_diary_entry_for_a_past_day_lands_at_local_noon()
    {
        var clock = new FakeClock(
            new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero), TestTimeZones.Fixed(2));
        var (fx, habitId) = await SeedJournalAsync(clock);
        var sut = NewService(fx, out _);
        var entryDay = new DateOnly(2026, 8, 15);

        await sut.SaveEntryAsync(habitId, entryDay, "ieri l'altro");

        await using var db = await fx.Factory.CreateDbContextAsync();
        var completion = await db.Completions.SingleAsync(c => c.HabitNodeId == habitId);
        completion.IsRetroactive.Should().BeTrue();
        completion.CompletedAt.Should().Be(new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc));
        // E, la cosa che conta davvero: l'istante appartiene al giorno nominato, non a quello prima.
        var boundary = await fx.ResetTimes.GetDayBoundaryAsync(habitId);
        boundary.DayOf(completion.CompletedAtMs).Should().Be(entryDay);
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

    [Fact]
    public async Task DeleteEntryAsync_by_note_id_removes_both_and_publishes_both_events()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out var bus);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "entry");
        Guid completionId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            completionId = (await db.Notes.SingleAsync(n => n.Id == noteId)).CompletionId!.Value;
        bus.Published.Clear();

        await sut.DeleteEntryAsync(noteId);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Notes.AnyAsync(n => n.Id == noteId)).Should().BeFalse();
            (await db.Completions.AnyAsync(c => c.Id == completionId)).Should().BeFalse();
            (await db.NoteHistories.AnyAsync(h => h.NoteId == noteId)).Should().BeFalse();
        }
        bus.Published.Should().Contain(p => p is NoteDeletedPayload);
        bus.Published.Should().Contain(p => p is CompletionDeletedPayload);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteEntryAsync_by_completion_id_removes_both()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "entry");
        Guid completionId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            completionId = (await db.Notes.SingleAsync(n => n.Id == noteId)).CompletionId!.Value;

        await sut.DeleteEntryAsync(completionId);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Notes.AnyAsync(n => n.Id == noteId)).Should().BeFalse();
            (await db.Completions.AnyAsync(c => c.Id == completionId)).Should().BeFalse();
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteEntryAsync_removes_attachments_without_throwing_FK_violation()
    {
        // attachment.note_id is FK Restrict (AttachmentConfiguration) — DeleteEntryAsync must clear
        // attachment rows before deleting the Note, or SQLite raises FOREIGN KEY constraint failed.
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "entry with photo");
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.Attachments.Add(new Recordum.Data.Models.Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                Filename = "photo.jpg",
                MimeType = "image/jpeg",
                FileSizeBytes = 123,
            });
            await db.SaveChangesAsync();
        }

        await sut.DeleteEntryAsync(noteId);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Notes.AnyAsync(n => n.Id == noteId)).Should().BeFalse();
            (await db.Attachments.CountAsync(a => a.NoteId == noteId)).Should().Be(0);
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteEntryAsync_one_of_two_same_day_keeps_the_day_complete()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var firstId = await sut.SaveEntryAsync(habitId, today, "first");
        await sut.SaveEntryAsync(habitId, today, "second");

        await sut.DeleteEntryAsync(firstId);

        await using var db = await fx.Factory.CreateDbContextAsync();
        // One note + one completion remain on the day → day stays "done".
        (await db.Notes.CountAsync(n => n.HabitNodeId == habitId)).Should().Be(1);
        (await db.Completions.CountAsync(c => c.HabitNodeId == habitId)).Should().Be(1);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task EditEntryAsync_updates_body_and_history_but_not_the_completion()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out var bus);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "v1");
        Guid completionId;
        long completedMs;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var n = await db.Notes.SingleAsync(x => x.Id == noteId);
            completionId = n.CompletionId!.Value;
            completedMs = (await db.Completions.SingleAsync(c => c.Id == completionId)).CompletedAtMs;
        }
        bus.Published.Clear();

        await sut.EditEntryAsync(noteId, "v2");

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var n = await db.Notes.SingleAsync(x => x.Id == noteId);
            n.BodyMd.Should().Be("v2");
            n.CompletionId.Should().Be(completionId);
            (await db.Completions.SingleAsync(c => c.Id == completionId)).CompletedAtMs.Should().Be(completedMs);
            (await db.NoteHistories.CountAsync(h => h.NoteId == noteId)).Should().Be(2); // v1 + v2
        }
        bus.Published.Should().Contain(p => p is NoteUpdatedPayload);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetEntryByCompletionAsync_returns_paired_note()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "find me");
        Guid completionId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            completionId = (await db.Notes.SingleAsync(n => n.Id == noteId)).CompletionId!.Value;

        var note = await sut.GetEntryByCompletionAsync(completionId);
        note.Should().NotBeNull();
        note!.Id.Should().Be(noteId);
        note.BodyMd.Should().Be("find me");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetEntrySummariesByCompletionsAsync_maps_completions_to_body_and_sorted_tags()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var n1 = await sut.SaveEntryAsync(habitId, today, new DiaryEntryDraft("first #zeta", ["alfa"], []));
        var n2 = await sut.SaveEntryAsync(habitId, today, "second");
        Guid c1, c2;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            c1 = (await db.Notes.SingleAsync(n => n.Id == n1)).CompletionId!.Value;
            c2 = (await db.Notes.SingleAsync(n => n.Id == n2)).CompletionId!.Value;
        }

        var map = await sut.GetEntrySummariesByCompletionsAsync(new[] { c1, c2, Guid.NewGuid() });

        map.Should().HaveCount(2);
        map[c1].NoteId.Should().Be(n1);
        map[c1].Body.Should().Be("first #zeta");
        map[c1].Tags.Should().Equal(new EntryTag("alfa", NoteTagSource.Property), new EntryTag("zeta", NoteTagSource.Inline));
        map[c2].NoteId.Should().Be(n2);
        map[c2].Body.Should().Be("second");
        map[c2].Tags.Should().BeEmpty();
        await fx.DisposeAsync();
    }
}
