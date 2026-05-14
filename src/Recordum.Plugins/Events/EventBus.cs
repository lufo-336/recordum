using System.Collections.Concurrent;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Plugins.Events;

public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<EventName, List<Action<EventEnvelope>>> _handlers = new();
    private readonly object _lock = new();

    public void Publish(IEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var envelope = new EventEnvelope(payload.Name, payload, DateTimeOffset.UtcNow);

        List<Action<EventEnvelope>>? snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(payload.Name, out var list)) return;
            snapshot = [..list];
        }

        foreach (var handler in snapshot)
        {
            try { handler(envelope); }
            catch { /* handler failures must not break the bus */ }
        }
    }

    public IDisposable Subscribe(EventName name, Action<EventEnvelope> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            var list = _handlers.GetOrAdd(name, _ => []);
            list.Add(handler);
        }
        return new Subscription(this, name, handler);
    }

    private void Remove(EventName name, Action<EventEnvelope> handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(name, out var list)) list.Remove(handler);
        }
    }

    private sealed class Subscription(EventBus bus, EventName name, Action<EventEnvelope> handler) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            bus.Remove(name, handler);
            _disposed = true;
        }
    }
}
