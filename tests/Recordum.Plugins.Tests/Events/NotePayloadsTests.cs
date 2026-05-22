using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Plugins.Tests.Events;

public class NotePayloadsTests
{
    [Fact]
    public void NoteCreatedPayload_Name_is_note_created()
    {
        var p = new NoteCreatedPayload(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(WellKnownEvents.NoteCreated, p.Name);
    }

    [Fact]
    public void NoteUpdatedPayload_Name_is_note_updated()
    {
        var p = new NoteUpdatedPayload(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(WellKnownEvents.NoteUpdated, p.Name);
    }

    [Fact]
    public void NoteDeletedPayload_Name_is_note_deleted()
    {
        var p = new NoteDeletedPayload(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(WellKnownEvents.NoteDeleted, p.Name);
    }
}
