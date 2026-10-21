using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public class AttachmentBackupRestoreTests
{
    private const string Pass = "passphrase-1234567890";

    [Fact]
    public async Task Restore_round_trip_recovers_note_attachment_row_and_file_bytes()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var attachments = new AttachmentService(
                fixture.DbFactory, fixture.Clock, new FakePluginContext());

            Guid noteId;
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "h" };
                db.HabitNodes.Add(habit);
                var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit.Id, BodyMd = "with a photo" };
                db.Notes.Add(note);
                await db.SaveChangesAsync();
                noteId = note.Id;
            }

            var photoBytes = "REAL-PHOTO-BYTES"u8.ToArray();
            var added = await attachments.AddAsync(noteId, new ImportedPhoto("source.jpg", "image/jpeg", photoBytes, [1]), default);

            // Backup, then wipe the live row + file to make the restore observable.
            var bk = await fixture.CreateBackupAsync(Pass);

            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                await db.Attachments.Where(a => a.NoteId == noteId).ExecuteDeleteAsync();

            var restore = fixture.NewRestoreService();
            var result = await restore.RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: true));
            result.UnavailablePlugins.Should().BeEmpty();

            var restoredRows = await attachments.ListByNoteAsync(noteId, default);
            restoredRows.Should().ContainSingle().Which.Id.Should().Be(added.Id);

            (await attachments.ReadContentAsync(added.Id, default)).Should().Equal(photoBytes);
            (await attachments.ReadThumbnailAsync(added.Id, default)).Should().Equal(1);
        }
        finally { await fixture.DisposeAsync(); }
    }
}
