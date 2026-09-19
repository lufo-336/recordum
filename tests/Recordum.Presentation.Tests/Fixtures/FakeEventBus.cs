using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeEventBus : IEventBus
{
    private readonly Dictionary<EventName, List<Action<EventEnvelope>>> _handlers = [];

    public List<IEventPayload> Published { get; } = [];

    public bool HasSubscribers => _handlers.Values.Any(l => l.Count > 0);

    public void Publish(IEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Published.Add(payload);
        if (_handlers.TryGetValue(payload.Name, out var list))
        {
            var envelope = new EventEnvelope(payload.Name, payload, DateTimeOffset.UtcNow);
            foreach (var h in list.ToArray()) h(envelope);
        }
    }

    public IDisposable Subscribe(EventName name, Action<EventEnvelope> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryGetValue(name, out var list))
        {
            list = [];
            _handlers[name] = list;
        }
        list.Add(handler);
        return new Subscription(() => list.Remove(handler));
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
