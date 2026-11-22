using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Schema;

public sealed class NoteTagSchemaTests
{
    private static async Task<(Guid Habit, Guid Note, Guid TagA, Guid TagB)> SeedAsync(DbFixture fx)
    {
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit.Id, BodyMd = "n", Category = NoteCategory.Diario };
        var a = new Tag { Id = Guid.NewGuid(), Name = "a" };
        var b = new Tag { Id = Guid.NewGuid(), Name = "b" };
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(habit);
        db.Notes.Add(note);
        db.Tags.AddRange(a, b);
        db.NoteTags.AddRange(
            new NoteTag { NoteId = note.Id, TagId = a.Id, Source = NoteTagSource.Inline },
            new NoteTag { NoteId = note.Id, TagId = b.Id, Source = NoteTagSource.Property });
        await db.SaveChangesAsync();
        return (habit.Id, note.Id, a.Id, b.Id);
    }

    [Fact]
    public async Task Deleting_a_note_removes_its_note_tags()
    {
        await using var fx = new DbFixture();
        var (_, noteId, _, _) = await SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();

        var deleted = await db.Notes.Where(n => n.Id == noteId).ExecuteDeleteAsync();

        Assert.Equal(1, deleted);
        Assert.Empty(await db.NoteTags.ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_tag_removes_its_note_tags_and_keeps_the_note()
    {
        await using var fx = new DbFixture();
        var (_, noteId, tagA, tagB) = await SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();

        await db.Tags.Where(t => t.Id == tagA).ExecuteDeleteAsync();

        Assert.Empty(await db.NoteTags.Where(nt => nt.TagId == tagA).ToListAsync());
        Assert.Single(await db.NoteTags.Where(nt => nt.TagId == tagB).ToListAsync());
        Assert.True(await db.Notes.AnyAsync(n => n.Id == noteId));
    }

    [Fact]
    public async Task Purging_a_habit_leaves_no_note_tags()
    {
        var clock = new FakeClock();
        await using var fx = new DbFixture(clock);
        var (habitId, _, _, _) = await SeedAsync(fx);

        await new PurgeService(fx.Factory, clock, NullLogger<PurgeService>.Instance).PurgeSubtreeAsync(habitId);

        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Empty(await db.NoteTags.ToListAsync());
        Assert.Empty(await db.Notes.ToListAsync());
    }

    [Fact]
    public async Task Same_pair_twice_needs_different_sources()
    {
        await using var fx = new DbFixture();
        var (_, noteId, tagA, _) = await SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();

        // (note, A) already exists as Inline: Property is a different row, fine.
        db.NoteTags.Add(new NoteTag { NoteId = noteId, TagId = tagA, Source = NoteTagSource.Property });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.NoteTags.CountAsync(nt => nt.TagId == tagA));

        // The same (note, A, Inline) again violates the primary key.
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        db2.NoteTags.Add(new NoteTag { NoteId = noteId, TagId = tagA, Source = NoteTagSource.Inline });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Unknown_source_violates_the_check()
    {
        await using var fx = new DbFixture();
        var (_, noteId, _, _) = await SeedAsync(fx);
        var tag = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.Tags.Add(new Tag { Id = tag, Name = "c" });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO note_tag (note_id, tag_id, source, updated_at_ms) VALUES ({noteId}, {tag}, 'Altro', 0)"));
        Assert.Contains("ck_note_tag_source", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attachment_content_of_one_megabyte_round_trips()
    {
        await using var fx = new DbFixture();
        var (_, noteId, _, _) = await SeedAsync(fx);
        var content = new byte[1024 * 1024];
        new Random(42).NextBytes(content);
        var thumb = new byte[] { 1, 2, 3 };
        var id = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.Attachments.Add(new Data.Models.Attachment
            {
                Id = id, NoteId = noteId, Filename = "p.jpg", MimeType = "image/jpeg",
                FileSizeBytes = content.Length, Content = content, Thumbnail = thumb,
            });
            await db.SaveChangesAsync();
        }

        await using var read = await fx.Factory.CreateDbContextAsync();
        var back = await read.Attachments.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal(content, back.Content);
        Assert.Equal(thumb, back.Thumbnail);
        Assert.False(back.IsMissing);
    }
}

public sealed class NoteTagMigrationTests(SchemaFixture schema) : IClassFixture<SchemaFixture>
{
    [Fact]
    public void Migrated_database_has_note_tag_cascades_and_attachment_blob_columns()
    {
        using var conn = schema.OpenRawConnection();
        static List<string> Rows(SqliteConnection c, string sql, int col)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            var list = new List<string>();
            while (r.Read()) list.Add(r.GetString(col));
            return list;
        }

        Assert.Equal(["CASCADE", "CASCADE"], Rows(conn, "PRAGMA foreign_key_list(note_tag)", 6));
        Assert.Contains("ix_note_tag_tag_id", Rows(conn, "PRAGMA index_list(note_tag)", 1));
        var cols = Rows(conn, "PRAGMA table_info(attachment)", 1);
        Assert.Contains("content", cols);
        Assert.Contains("thumbnail", cols);
        Assert.Contains("is_missing", cols);
    }
}
