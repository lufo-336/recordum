using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Tests.Fixtures;

public sealed class RecordingEventBus : IEventBus
{
    public List<IEventPayload> Published { get; } = [];

    public void Publish(IEventPayload payload) => Published.Add(payload);

    public IDisposable Subscribe(EventName name, Action<EventEnvelope> handler) => new NullDisposable();

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
