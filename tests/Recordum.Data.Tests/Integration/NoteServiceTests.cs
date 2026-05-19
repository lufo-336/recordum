using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class NoteServiceTests
{
    [Fact]
    public async Task Create_persists_note_and_attachments_only_UpdatedAtMs()
    {
        await using var fx = new DbFixture();
        var hid = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = hid, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" });
            await db.SaveChangesAsync();
        }
        var svc = new NoteService(fx.Factory, fx.Clock, NullLogger<NoteService>.Instance);
        var id = await svc.CreateAsync(
            new Note { HabitNodeId = hid, BodyMd = "hi" },
            new[] { new Attachment { Id = Guid.NewGuid(), FilePath = "/tmp/a.png", MimeType = "image/png" } });
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        var note = await db2.Notes.Include(n => n.Attachments).SingleAsync();
        Assert.Equal(id, note.Id);
        Assert.Single(note.Attachments);
        Assert.True(note.UpdatedAtMs > 0);
    }
}
