using System;
using System.IO;
using System.Threading.Tasks;
using Recordum.Core;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class AttachmentServiceTests : IAsyncLifetime
{
    private DbFixture _fixture = null!;
    private IAttachmentService _service = null!;
    private string _tempDir = null!;

    public Task InitializeAsync()
    {
        _fixture = new DbFixture();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _service = new AttachmentService(
            _fixture.Factory,
            _fixture.Clock,
            _fixture.PluginContext);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        await _fixture.DisposeAsync();
    }

    private async Task<Guid> SeedNoteAsync()
    {
        Guid noteId;
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var habit = new HabitNode
            {
                Id = Guid.NewGuid(),
                NodeType = Core.Enums.NodeType.Habit,
                HabitType = Core.Enums.HabitType.Journal,
                Name = "h",
            };
            db.HabitNodes.Add(habit);
            var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit.Id, BodyMd = "x" };
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            noteId = note.Id;
        }
        return noteId;
    }

    private static ImportedPhoto Photo(string name = "test.jpg", string mime = "image/jpeg", byte[]? content = null) =>
        new(Guid.NewGuid(), name, mime, content ?? [1, 2, 3, 4], [9, 9]);

    [Fact]
    public async Task AddAsync_stores_content_and_thumbnail_in_the_row_and_nothing_on_disk()
    {
        var noteId = await SeedNoteAsync();

        var attachment = await _service.AddAsync(noteId, Photo(content: [7, 7, 7]), default);

        Assert.Equal(noteId, attachment.NoteId);
        Assert.Equal("image/jpeg", attachment.MimeType);
        Assert.Equal(3, attachment.FileSizeBytes);
        Assert.Equal(_fixture.Clock.UtcNow, attachment.CreatedAt);
        Assert.Equal(_fixture.Clock.UtcNowMs, attachment.UpdatedAtMs);

        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var row = await db.Attachments.FindAsync(attachment.Id);
            Assert.Equal(new byte[] { 7, 7, 7 }, row!.Content);
            Assert.Equal(new byte[] { 9, 9 }, row.Thumbnail);
        }
        Assert.Equal(new byte[] { 7, 7, 7 }, await _service.ReadContentAsync(attachment.Id, default));
        Assert.Equal(new byte[] { 9, 9 }, await _service.ReadThumbnailAsync(attachment.Id, default));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "attachments")));
    }

    [Fact]
    public async Task Read_returns_null_for_an_unknown_id()
    {
        Assert.Null(await _service.ReadContentAsync(Guid.NewGuid(), default));
        Assert.Null(await _service.ReadThumbnailAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task ListByNoteAsync_returns_all_for_note_without_selecting_the_blob_columns()
    {
        var noteId = await SeedNoteAsync();
        await _service.AddAsync(noteId, Photo("a.jpg"), default);
        await _service.AddAsync(noteId, Photo("b.png", "image/png"), default);

        lock (_fixture.Sql) _fixture.Sql.Clear();
        var attachments = await _service.ListByNoteAsync(noteId, default);

        Assert.Equal(2, attachments.Count);
        var select = Assert.Single(_fixture.Sql, q => q.Contains("FROM \"attachment\"", StringComparison.Ordinal));
        // HasContent chiede solo se l'originale c'è (IS NOT NULL): i byte non si leggono.
        Assert.DoesNotMatch("\"content\"(?!\\s+IS NOT NULL)", select);
        Assert.Contains("\"content\" IS NOT NULL", select, StringComparison.Ordinal);
        Assert.DoesNotContain("thumbnail", select, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAllAsync_returns_attachments_across_notes()
    {
        var noteId1 = await SeedNoteAsync();
        var noteId2 = await SeedNoteAsync();
        await _service.AddAsync(noteId1, Photo(), default);
        await _service.AddAsync(noteId2, Photo(), default);

        var all = await _service.ListAllAsync(default);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_row()
    {
        var noteId = await SeedNoteAsync();
        var attachment = await _service.AddAsync(noteId, Photo(), default);

        await _service.DeleteAsync(attachment.Id, default);

        await using (var db = await _fixture.Factory.CreateDbContextAsync())
            Assert.Null(await db.Attachments.FindAsync(attachment.Id));
        Assert.Empty(await _service.ListByNoteAsync(noteId, default));
    }

    [Fact]
    public async Task DeleteAsync_is_a_no_op_when_attachment_missing()
    {
        await _service.DeleteAsync(Guid.NewGuid(), default);
    }

    [Fact]
    public async Task ResolveRelativePath_lowercases_the_extension_and_falls_back_to_bin()
    {
        var noteId = await SeedNoteAsync();
        var upper = await _service.AddAsync(noteId, Photo("photo.JPG"), default);
        var bare = await _service.AddAsync(noteId, Photo("photo_no_ext"), default);

        Assert.EndsWith(".jpg", _service.ResolveRelativePath(upper), StringComparison.Ordinal);
        Assert.EndsWith(".bin", _service.ResolveRelativePath(bare), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddAsync_stamps_the_device_name()
    {
        var noteId = await SeedNoteAsync();
        var identity = new FakeDeviceIdentity { DeviceName = "Studio" };
        var svc = new AttachmentService(_fixture.Factory, _fixture.Clock, _fixture.PluginContext, identity);

        var attachment = await svc.AddAsync(noteId, Photo(), default);

        Assert.Equal("Studio", attachment.OriginDeviceName);
        Assert.True(attachment.HasContent);
        var listed = Assert.Single(await svc.ListByNoteAsync(noteId, default));
        Assert.Equal("Studio", listed.OriginDeviceName);
        Assert.True(listed.HasContent);
    }

    [Fact]
    public async Task AddAsync_without_identity_leaves_origin_null()
    {
        var noteId = await SeedNoteAsync();
        var identity = new FakeDeviceIdentity { DeviceName = null };
        var svc = new AttachmentService(_fixture.Factory, _fixture.Clock, _fixture.PluginContext, identity);

        var attachment = await svc.AddAsync(noteId, Photo(), default);

        Assert.Null(attachment.OriginDeviceName);
        var listed = Assert.Single(await svc.ListByNoteAsync(noteId, default));
        Assert.Null(listed.OriginDeviceName);
    }

    [Fact]
    public async Task Project_reports_HasContent_false_when_content_is_null()
    {
        var noteId = await SeedNoteAsync();
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            db.Attachments.Add(new Models.Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                Filename = "remote.jpg",
                MimeType = "image/jpeg",
                FileSizeBytes = 100,
                Content = null,
                Thumbnail = [1, 2],
                OriginDeviceName = "Telefono",
            });
            await db.SaveChangesAsync();
        }

        var listed = Assert.Single(await _service.ListByNoteAsync(noteId, default));
        Assert.False(listed.HasContent);
        Assert.Equal("Telefono", listed.OriginDeviceName);
    }
}
