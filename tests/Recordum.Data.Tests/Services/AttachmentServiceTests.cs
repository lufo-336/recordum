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
            _fixture.PluginContext,
            _tempDir); // Pass custom temp directory for testing
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

    [Fact]
    public async Task AddAsync_copies_file_and_inserts_row()
    {
        var sourceFile = Path.Combine(_tempDir, "test.jpg");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        var noteId = await SeedNoteAsync();

        var attachment = await _service.AddAsync(noteId, sourceFile, default);

        Assert.Equal(noteId, attachment.NoteId);
        Assert.Equal("image/jpeg", attachment.MimeType);
        Assert.True(attachment.FileSizeBytes > 0);
        Assert.True(attachment.CreatedAt > DateTimeOffset.MinValue);
        Assert.Equal(_fixture.Clock.UtcNow, attachment.CreatedAt);
        Assert.Equal(_fixture.Clock.UtcNow, attachment.UpdatedAt);
        Assert.Equal(_fixture.Clock.UtcNowMs, attachment.UpdatedAtMs);

        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var row = await db.Attachments.FindAsync(attachment.Id);
            Assert.NotNull(row);
            Assert.Equal(noteId, row!.NoteId);
            Assert.Equal(".jpg", Path.GetExtension(row.Filename));
        }

        var expectedPath = Path.Combine(_tempDir, "attachments", noteId.ToString(), attachment.Id.ToString("N") + ".jpg");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task AddAsync_throws_when_source_file_missing()
    {
        var noteId = await SeedNoteAsync();
        var missing = Path.Combine(_tempDir, "does-not-exist.png");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.AddAsync(noteId, missing, default));
    }

    [Fact]
    public async Task ListByNoteAsync_returns_all_for_note()
    {
        var noteId = await SeedNoteAsync();

        var file1 = Path.Combine(_tempDir, "photo1.jpg");
        var file2 = Path.Combine(_tempDir, "photo2.png");
        await File.WriteAllTextAsync(file1, "a");
        await File.WriteAllTextAsync(file2, "b");

        await _service.AddAsync(noteId, file1, default);
        await _service.AddAsync(noteId, file2, default);

        var attachments = await _service.ListByNoteAsync(noteId, default);

        Assert.Equal(2, attachments.Count);
    }

    [Fact]
    public async Task ListAllAsync_returns_attachments_across_notes()
    {
        var noteId1 = await SeedNoteAsync();
        var noteId2 = await SeedNoteAsync();

        var file1 = Path.Combine(_tempDir, "a.jpg");
        var file2 = Path.Combine(_tempDir, "b.jpg");
        await File.WriteAllTextAsync(file1, "a");
        await File.WriteAllTextAsync(file2, "b");

        await _service.AddAsync(noteId1, file1, default);
        await _service.AddAsync(noteId2, file2, default);

        var all = await _service.ListAllAsync(default);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task DeleteAsync_removes_row_and_file()
    {
        var noteId = await SeedNoteAsync();
        var sourceFile = Path.Combine(_tempDir, "photo.jpg");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        var attachment = await _service.AddAsync(noteId, sourceFile, default);
        var diskPath = Path.Combine(_tempDir, "attachments", noteId.ToString(), attachment.Id.ToString("N") + ".jpg");
        Assert.True(File.Exists(diskPath));

        await _service.DeleteAsync(attachment.Id, default);

        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var row = await db.Attachments.FindAsync(attachment.Id);
            Assert.Null(row);
        }
        Assert.False(File.Exists(diskPath));

        var remaining = await _service.ListByNoteAsync(noteId, default);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task DeleteAsync_is_a_no_op_when_attachment_missing()
    {
        // Should not throw for an unknown id (idempotent delete).
        await _service.DeleteAsync(Guid.NewGuid(), default);
    }

    [Fact]
    public async Task DeleteAsync_does_not_throw_when_file_already_gone()
    {
        var noteId = await SeedNoteAsync();
        var sourceFile = Path.Combine(_tempDir, "photo.jpg");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        var attachment = await _service.AddAsync(noteId, sourceFile, default);
        var diskPath = Path.Combine(_tempDir, "attachments", noteId.ToString(), attachment.Id.ToString("N") + ".jpg");
        File.Delete(diskPath); // simulate the file already missing from disk

        await _service.DeleteAsync(attachment.Id, default);

        await using var db = await _fixture.Factory.CreateDbContextAsync();
        Assert.Null(await db.Attachments.FindAsync(attachment.Id));
    }

    [Fact]
    public async Task ResolveAbsolutePath_finds_the_file_AddAsync_wrote_for_uppercase_extension()
    {
        var noteId = await SeedNoteAsync();
        var sourceFile = Path.Combine(_tempDir, "photo.JPG");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        var attachment = await _service.AddAsync(noteId, sourceFile, default);

        var relative = _service.ResolveRelativePath(attachment);
        var absolute = _service.ResolveAbsolutePath(attachment);

        // Normalized to lowercase regardless of the original file's casing.
        Assert.EndsWith(".jpg", relative, StringComparison.Ordinal);
        Assert.True(File.Exists(absolute));
        Assert.Equal(Path.Combine(_tempDir, "attachments", noteId.ToString(), attachment.Id.ToString("N") + ".jpg"), absolute);
    }

    [Fact]
    public async Task ResolveAbsolutePath_finds_the_file_AddAsync_wrote_for_extensionless_original()
    {
        var noteId = await SeedNoteAsync();
        var sourceFile = Path.Combine(_tempDir, "photo_no_ext");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        var attachment = await _service.AddAsync(noteId, sourceFile, default);

        var relative = _service.ResolveRelativePath(attachment);
        var absolute = _service.ResolveAbsolutePath(attachment);

        Assert.EndsWith(".bin", relative, StringComparison.Ordinal);
        Assert.True(File.Exists(absolute));
    }

    [Fact]
    public async Task DeleteAsync_removes_file_when_original_had_no_extension()
    {
        // Regression for the DeleteAsync bug: it used to re-derive the extension straight from
        // entity.Filename (no lowercasing, no "bin" fallback), so for an extensionless original it
        // computed a path with no extension at all while AddAsync had actually written "<id>.bin" —
        // the delete would silently miss the real file.
        var noteId = await SeedNoteAsync();
        var sourceFile = Path.Combine(_tempDir, "photo_no_ext");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        var attachment = await _service.AddAsync(noteId, sourceFile, default);
        var diskPath = Path.Combine(_tempDir, "attachments", noteId.ToString(), attachment.Id.ToString("N") + ".bin");
        Assert.True(File.Exists(diskPath));

        await _service.DeleteAsync(attachment.Id, default);

        Assert.False(File.Exists(diskPath));
    }
}
