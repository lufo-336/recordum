using FluentAssertions;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Events;
using Recordum.Presentation.Services;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class UiEventSubscriptionExtensionsTests
{
    [Fact]
    public void SubscribeOnUi_forwards_published_events_to_the_handler()
    {
        // With no Application (test host), there is no dispatcher to marshal onto, so the handler
        // runs inline — the subscription must still deliver the event.
        var bus = new EventBus();
        EventEnvelope? received = null;
        using var sub = bus.SubscribeOnUi(WellKnownEvents.CompletionRecorded, e => received = e);

        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));

        received.Should().NotBeNull();
    }

    [Fact]
    public void SubscribeOnUi_disposing_unsubscribes()
    {
        var bus = new EventBus();
        var count = 0;
        var sub = bus.SubscribeOnUi(WellKnownEvents.CompletionRecorded, _ => count++);

        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));
        sub.Dispose();
        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));

        count.Should().Be(1);
    }
}
