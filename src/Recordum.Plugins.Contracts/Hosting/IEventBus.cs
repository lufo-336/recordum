using Recordum.Plugins.Contracts.Events;

namespace Recordum.Plugins.Contracts.Hosting;

public interface IEventBus
{
    void Publish(IEventPayload payload);
    IDisposable Subscribe(EventName name, Action<EventEnvelope> handler);
}
