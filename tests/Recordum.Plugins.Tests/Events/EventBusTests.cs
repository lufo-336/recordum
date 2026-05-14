using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Events;
using Xunit;

namespace Recordum.Plugins.Tests.Events;

public class EventBusTests
{
    private static readonly EventName NameA = new("habit.completed");
    private static readonly EventName NameB = new("habit.deleted");

    private sealed record FakePayload(EventName Name) : IEventPayload;

    [Fact]
    public void Subscribe_then_Publish_delivers_envelope_to_handler()
    {
        var bus = new EventBus();
        EventEnvelope? received = null;
        bus.Subscribe(NameA, env => received = env);

        var payload = new FakePayload(NameA);
        bus.Publish(payload);

        Assert.NotNull(received);
        Assert.Equal(NameA, received.Name);
        Assert.Same(payload, received.Payload);
        Assert.True(DateTimeOffset.UtcNow - received.OccurredAt < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Dispose_subscription_stops_delivery()
    {
        var bus = new EventBus();
        int callCount = 0;
        var subscription = bus.Subscribe(NameA, _ => callCount++);

        bus.Publish(new FakePayload(NameA));
        subscription.Dispose();
        bus.Publish(new FakePayload(NameA));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Multiple_subscribers_all_receive_the_event()
    {
        var bus = new EventBus();
        int count1 = 0, count2 = 0, count3 = 0;

        bus.Subscribe(NameA, _ => count1++);
        bus.Subscribe(NameA, _ => count2++);
        bus.Subscribe(NameA, _ => count3++);

        bus.Publish(new FakePayload(NameA));

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
        Assert.Equal(1, count3);
    }

    [Fact]
    public void Throwing_handler_does_not_break_delivery_to_other_handlers()
    {
        var bus = new EventBus();
        int goodCount = 0;

        bus.Subscribe(NameA, _ => throw new Exception("boom"));
        bus.Subscribe(NameA, _ => goodCount++);

        bus.Publish(new FakePayload(NameA));

        Assert.Equal(1, goodCount);
    }

    [Fact]
    public void Unrelated_event_name_does_not_trigger_handler()
    {
        var bus = new EventBus();
        int callCount = 0;
        bus.Subscribe(NameA, _ => callCount++);

        bus.Publish(new FakePayload(NameB));

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Publish_null_payload_throws_ArgumentNullException()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Publish(null!));
    }

    [Fact]
    public void Subscribe_null_handler_throws_ArgumentNullException()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Subscribe(NameA, null!));
    }
}
