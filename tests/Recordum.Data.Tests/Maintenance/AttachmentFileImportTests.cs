using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Maintenance;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using SkiaSharp;
using Xunit;
using Attachment = Recordum.Data.Models.Attachment;

namespace Recordum.Data.Tests.Maintenance;

/// <summary>F2d-1 Task 5: i file del vecchio layout entrano nel DB; un file si cancella solo dopo il commit,
/// se la riga ha già il contenuto, o se è un Guid senza riga. Tutto il resto resta.</summary>
public sealed class AttachmentFileImportTests : IAsyncDisposable
{
    private readonly DbFixture _fx = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "recordum-attimp-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _noteId = Guid.NewGuid();

    public AttachmentFileImportTests() => Directory.CreateDirectory(_root);

    public async ValueTask DisposeAsync()
    {
        await _fx.DisposeAsync();
        if (OperatingSystem.IsWindows() == false)
            foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetUnixFileMode(f, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.Delete(_root, recursive: true);
    }

    private AttachmentFileImport Sut(IDbContextFactory<RecordumDbContext>? factory = null) => new(
        factory ?? _fx.Factory, new SkiaPhotoImporter(new AppSettingService(_fx.Factory, _fx.Clock, NullLogger<AppSettingService>.Instance)),
        new EncryptedDbFixture.TestBackupPaths("", "", "", _root, "", "", ""),
        NullLogger<AttachmentFileImport>.Instance);

    private async Task SeedNoteAsync()
    {
        await using var db = await _fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        db.HabitNodes.Add(h);
        db.Notes.Add(new Note { Id = _noteId, HabitNodeId = h.Id, BodyMd = "x" });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedRowAsync(byte[]? content = null)
    {
        await SeedNoteAsync().ContinueWith(_ => { }, TaskContinuationOptions.None);
        return await AddRowAsync(content);
    }

    private async Task<Guid> AddRowAsync(byte[]? content)
    {
        await using var db = await _fx.Factory.CreateDbContextAsync();
        var a = new Attachment { Id = Guid.NewGuid(), NoteId = _noteId, Filename = "p.png", MimeType = "image/png", Content = content };
        db.Attachments.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private string PathOf(Guid id, string ext = "png") => Path.Combine(_root, _noteId.ToString(), id.ToString("N") + "." + ext);

    private string WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Png()
    {
        using var bmp = new SKBitmap(8, 8);
        bmp.Erase(SKColors.Red);
        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(SKEncodedImageFormat.Png, 90).ToArray();
    }

    private async Task<Attachment> RowAsync(Guid id)
    {
        await using var db = await _fx.Factory.CreateDbContextAsync();
        return await db.Attachments.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    [Fact]
    public async Task Row_without_content_takes_the_file_bytes_and_a_thumbnail_then_the_file_goes()
    {
        var id = await SeedRowAsync();
        var bytes = Png();
        var path = WriteFile(PathOf(id), bytes);

        var report = await Sut().RunAsync(default);

        report.Should().Be(new AttachmentImportReport(1, 0, 0, 0, 0));
        var row = await RowAsync(id);
        row.Content.Should().Equal(bytes);
        row.Thumbnail.Should().NotBeNull();
        File.Exists(path).Should().BeFalse();
        Directory.Exists(Path.GetDirectoryName(path)).Should().BeFalse("empty note folder is removed");
    }

    [Fact]
    public async Task Row_with_content_and_a_file_leaves_content_untouched_and_deletes_the_file()
    {
        var kept = new byte[] { 9, 9, 9 };
        var id = await SeedRowAsync(kept);
        var path = WriteFile(PathOf(id), Png());

        var report = await Sut().RunAsync(default);

        report.Should().Be(new AttachmentImportReport(0, 1, 0, 0, 0));
        (await RowAsync(id)).Content.Should().Equal(kept);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task Guid_named_file_without_a_row_is_deleted()
    {
        var path = WriteFile(PathOf(Guid.NewGuid()), Png());

        var report = await Sut().RunAsync(default);

        report.Should().Be(new AttachmentImportReport(0, 0, 1, 0, 0));
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task File_that_is_not_a_guid_stays()
    {
        var path = WriteFile(Path.Combine(_root, _noteId.ToString(), "appunti.txt"), [1]);

        var report = await Sut().RunAsync(default);

        report.Should().Be(new AttachmentImportReport(0, 0, 0, 1, 0));
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "UnixOnly")]
    public async Task Unreadable_file_stays_and_its_row_is_neither_deleted_nor_marked_missing()
    {
        var id = await SeedRowAsync();
        var path = WriteFile(PathOf(id), Png());
        File.SetUnixFileMode(path, UnixFileMode.None);

        var report = await Sut().RunAsync(default);

        report.Should().Be(new AttachmentImportReport(0, 0, 0, 1, 0));
        File.Exists(path).Should().BeTrue();
        var row = await RowAsync(id);
        row.IsMissing.Should().BeFalse();
        row.Content.Should().BeNull();
    }

    [Fact]
    public async Task Row_without_content_and_without_file_becomes_missing()
    {
        var id = await SeedRowAsync();

        var report = await Sut().RunAsync(default);

        report.Should().Be(new AttachmentImportReport(0, 0, 0, 0, 1));
        (await RowAsync(id)).IsMissing.Should().BeTrue();
    }

    [Fact]
    public async Task Second_run_reports_all_zero()
    {
        var withFile = await SeedRowAsync();
        WriteFile(PathOf(withFile), Png());
        await AddRowAsync(null); // no file -> missing
        WriteFile(PathOf(Guid.NewGuid()), Png());

        (await Sut().RunAsync(default)).Should().Be(new AttachmentImportReport(1, 0, 1, 0, 1));
        (await Sut().RunAsync(default)).Should().Be(new AttachmentImportReport(0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Readable_but_undecodable_file_still_enters_with_a_null_thumbnail()
    {
        var id = await SeedRowAsync();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var path = WriteFile(PathOf(id), bytes);

        var report = await Sut().RunAsync(default);

        report.Imported.Should().Be(1);
        var row = await RowAsync(id);
        row.Content.Should().Equal(bytes);
        row.Thumbnail.Should().BeNull();
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task A_failing_commit_leaves_the_file_where_it_is()
    {
        var id = await SeedRowAsync();
        var path = WriteFile(PathOf(id), Png());

        await using (var db = await _fx.Factory.CreateDbContextAsync())
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER boom BEFORE UPDATE ON attachment BEGIN SELECT RAISE(ABORT, 'commit failed'); END");

        var act = () => Sut().RunAsync(default);

        await act.Should().ThrowAsync<Exception>();
        File.Exists(path).Should().BeTrue("the file is deleted only after its row's commit");
        (await RowAsync(id)).Content.Should().BeNull();
    }
}
