using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class DiaryServicePhotoTests
{
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static ImportedPhoto Photo(string name, byte b) => new(Guid.NewGuid(), name, "image/jpeg", [b, b, b], [b]);

    private static async Task<(DbFixture fx, Guid habitId, DiaryService sut)> SetUpAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id, new DiaryService(fx.Factory, fx.Clock, NullLogger<DiaryService>.Instance, new RecordingEventBus(), fx.ResetTimes));
    }

    private static async Task<List<Recordum.Data.Models.Attachment>> RowsOf(DbFixture fx, Guid noteId)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        return await db.Attachments.AsNoTracking().Where(a => a.NoteId == noteId).OrderBy(a => a.Filename).ToListAsync();
    }

    [Fact]
    public async Task Save_with_two_photos_writes_two_rows_with_content_and_thumbnail()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("pranzo", [], [Photo("a.jpg", 1), Photo("b.jpg", 2)]));

        var rows = await RowsOf(fx, id);
        rows.Select(r => r.Filename).Should().Equal("a.jpg", "b.jpg");
        rows[0].Content.Should().Equal(1, 1, 1);
        rows[0].Thumbnail.Should().Equal(1);
        rows[1].Content.Should().Equal(2, 2, 2);
        rows[1].FileSizeBytes.Should().Be(3);
    }

    [Fact]
    public async Task Saved_attachment_row_keeps_the_id_of_the_imported_photo()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var photo = Photo("a.jpg", 1);
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("pranzo", [], [photo]));

        (await RowsOf(fx, id)).Single().Id.Should().Be(photo.Id);
    }

    [Fact]
    public async Task A_failing_photo_insert_rolls_back_the_note_too()
    {
        var (fx, habit, sut) = await SetUpAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_photo BEFORE INSERT ON attachment BEGIN SELECT RAISE(ABORT, 'boom'); END;");

        var act = async () => await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("x", [], [Photo("a.jpg", 1)]));

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var check = await fx.Factory.CreateDbContextAsync();
        (await check.Notes.CountAsync()).Should().Be(0, "nota, voce e foto sono una sola SaveChanges");
        (await check.Completions.CountAsync()).Should().Be(0);
        (await check.Attachments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Edit_adds_one_photo_and_removes_another()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("x", [], [Photo("a.jpg", 1), Photo("b.jpg", 2)]));
        var a = (await RowsOf(fx, id)).Single(r => r.Filename == "a.jpg");

        await sut.EditEntryAsync(id, new DiaryEntryEdit("x", null, [Photo("c.jpg", 3)], [a.Id]));

        (await RowsOf(fx, id)).Select(r => r.Filename).Should().Equal("b.jpg", "c.jpg");
    }

    [Fact]
    public async Task Edit_with_only_photo_changes_still_saves()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("x", [], []));

        await sut.EditEntryAsync(id, new DiaryEntryEdit("x", null, [Photo("a.jpg", 1)], []));

        (await RowsOf(fx, id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Edit_ignores_a_removed_id_that_belongs_to_another_note()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var mine = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("mia", [], []));
        var other = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("altrui", [], [Photo("z.jpg", 9)]));
        var foreign = (await RowsOf(fx, other)).Single();

        await sut.EditEntryAsync(mine, new DiaryEntryEdit("mia", null, [], [foreign.Id]));

        (await RowsOf(fx, other)).Should().ContainSingle("mai cancellare foto altrui");
    }

    [Fact]
    public async Task Edit_in_a_failing_transaction_keeps_the_photo_it_meant_to_remove()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("x", [], [Photo("a.jpg", 1)]));
        var a = (await RowsOf(fx, id)).Single();
        await using (var db = await fx.Factory.CreateDbContextAsync())
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_photo BEFORE INSERT ON attachment BEGIN SELECT RAISE(ABORT, 'boom'); END;");

        var act = async () => await sut.EditEntryAsync(id, new DiaryEntryEdit("x", null, [Photo("n.jpg", 5)], [a.Id]));

        await act.Should().ThrowAsync<DbUpdateException>();
        (await RowsOf(fx, id)).Select(r => r.Filename).Should().Equal("a.jpg");
    }

    [Fact]
    public async Task DeleteEntry_removes_the_photos_of_the_note()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("x", [], [Photo("a.jpg", 1), Photo("b.jpg", 2)]));

        await sut.DeleteEntryAsync(id);

        (await RowsOf(fx, id)).Should().BeEmpty();
    }
}
