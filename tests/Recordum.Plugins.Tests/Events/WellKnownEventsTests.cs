using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Plugins.Tests.Events;

public class WellKnownEventsTests
{
    [Fact]
    public void NoteDeleted_event_name_is_dotted_lowercase()
    {
        Assert.Equal("note.deleted", WellKnownEvents.NoteDeleted.Value);
    }
}
