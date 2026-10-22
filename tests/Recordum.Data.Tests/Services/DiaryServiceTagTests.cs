using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Interceptors;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class DiaryServiceTagTests
{
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static async Task<(DbFixture fx, Guid habitId, DiaryService sut)> SetUpAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id, NewService(fx, fx.Factory));
    }

    private static DiaryService NewService(DbFixture fx, IDbContextFactory<RecordumDbContext> factory)
        => new(factory, fx.Clock, NullLogger<DiaryService>.Instance, new RecordingEventBus(), fx.ResetTimes);

    private static async Task<IReadOnlyList<EntryTag>> TagsOf(DiaryService sut, Guid noteId)
        => (await sut.GetEntryTagsAsync([noteId])).GetValueOrDefault(noteId) ?? [];

    private static async Task<List<string>> AllTagNames(DbFixture fx)
    {
        await using var db = await fx.Factory.CreateDbContextAsync();
        return await db.Tags.Select(t => t.Name).OrderBy(n => n).ToListAsync();
    }

    [Fact]
    public async Task Save_writes_inline_and_property_tags_and_creates_them()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("pasta #pranzo", ["casa"], []));

        (await TagsOf(sut, id)).Should().Equal(
            new EntryTag("casa", NoteTagSource.Property), new EntryTag("pranzo", NoteTagSource.Inline));
        (await AllTagNames(fx)).Should().Equal("casa", "pranzo");
    }

    [Fact]
    public async Task An_existing_tag_is_not_duplicated_by_a_different_case()
    {
        var (fx, habit, sut) = await SetUpAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "Pranzo" });
            await db.SaveChangesAsync();
        }
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("#pranzo", [], []));

        (await AllTagNames(fx)).Should().Equal("Pranzo");
        (await TagsOf(sut, id)).Should().Equal(new EntryTag("Pranzo", NoteTagSource.Inline));
    }

    [Fact]
    public async Task Removing_a_tag_from_the_text_drops_it_from_the_entry_but_not_from_the_list()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("#pranzo", [], []));

        await sut.EditEntryAsync(id, new DiaryEntryEdit("niente", null, [], []));

        (await TagsOf(sut, id)).Should().BeEmpty();
        (await AllTagNames(fx)).Should().Equal("pranzo");
    }

    [Fact]
    public async Task The_old_text_only_edit_leaves_property_tags_alone()
    {
        var (_, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("a", ["casa"], []));

        await ((IDiaryService)sut).EditEntryAsync(id, "b #x");

        (await TagsOf(sut, id)).Should().Equal(
            new EntryTag("casa", NoteTagSource.Property), new EntryTag("x", NoteTagSource.Inline));
    }

    [Fact]
    public async Task Same_text_with_a_different_Tag_row_updates_tags_without_a_new_version()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("a", ["casa"], []));

        await sut.EditEntryAsync(id, new DiaryEntryEdit("a", ["lavoro"], [], []));

        (await TagsOf(sut, id)).Should().Equal(new EntryTag("lavoro", NoteTagSource.Property));
        await using var db = await fx.Factory.CreateDbContextAsync();
        (await db.NoteHistories.CountAsync(h => h.NoteId == id)).Should().Be(1);
    }

    [Fact]
    public async Task An_empty_or_too_long_Tag_row_name_is_discarded()
    {
        var (_, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day,
            new DiaryEntryDraft("a", ["  ", new string('x', 65), " ok "], []));

        (await TagsOf(sut, id)).Should().Equal(new EntryTag("ok", NoteTagSource.Property));
    }

    [Fact]
    public async Task A_failing_save_leaves_neither_the_note_nor_the_new_tag()
    {
        var fx = new DbFixture();
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var thrower = new ThrowingInterceptor();
        var factory = new Factory(conn, new TimestampInterceptor(fx.Clock, fx.PluginContext), thrower);
        await using (var setup = factory.CreateDbContext())
        {
            await setup.Database.EnsureCreatedAsync();
            setup.HabitNodes.Add(new HabitNode { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" });
            await setup.SaveChangesAsync();
        }
        var sut = NewService(fx, factory);
        thrower.Armed = true;

        var act = async () => await sut.SaveEntryAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), Day, new DiaryEntryDraft("#nuovo", ["altro"], []));

        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var db = factory.CreateDbContext();
        (await db.Notes.CountAsync()).Should().Be(0);
        (await db.Tags.CountAsync()).Should().Be(0);
    }

    private sealed class ThrowingInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
            => Armed ? throw new InvalidOperationException("boom") : new(result);
    }

    private sealed class Factory(SqliteConnection conn, params IInterceptor[] interceptors)
        : IDbContextFactory<RecordumDbContext>
    {
        public RecordumDbContext CreateDbContext()
            => new(new DbContextOptionsBuilder<RecordumDbContext>()
                .UseSqlite(conn).UseSnakeCaseNamingConvention().AddInterceptors(interceptors).Options);
    }

    [Fact]
    public async Task A_tags_only_edit_raises_the_note_UpdatedAtMs_for_the_sync()
    {
        var (fx, habit, sut) = await SetUpAsync();
        var id = await sut.SaveEntryAsync(habit, Day, new DiaryEntryDraft("a", ["casa"], []));
        ((Fixtures.FakeClock)fx.Clock).Advance(TimeSpan.FromMinutes(5));

        await sut.EditEntryAsync(id, new DiaryEntryEdit("a", ["lavoro"], [], []));

        await using var db2 = await fx.Factory.CreateDbContextAsync();
        (await db2.Notes.AsNoTracking().SingleAsync(n => n.Id == id)).UpdatedAtMs.Should().Be(fx.Clock.UtcNowMs);
    }
}
