using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Presentation.Services;

/// <summary>
/// Subscribes to <see cref="IEventBus"/> events with marshaling onto the UI thread.
/// </summary>
/// <remarks>
/// <see cref="EventBus"/> invokes handlers synchronously on the publisher's thread, which is often a
/// thread-pool thread (completion/note services record with <c>ConfigureAwait(false)</c>). View-model
/// handlers that touch view-bound <c>ObservableCollection</c>s would then mutate them off the UI thread
/// and crash with a cross-thread <see cref="System.NotSupportedException"/>. Marshaling at the
/// subscription boundary keeps the platform-agnostic bus (reused by web/MAUI) free of UI-framework concerns.
/// See phase-4 issue B-uiThread.
/// </remarks>
public static class UiEventSubscriptionExtensions
{
    public static IDisposable SubscribeOnUi(this IEventBus bus, EventName name, Action<EventEnvelope> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        // Capture the synchronization context of the thread that subscribes. View models are
        // constructed on the UI thread, so this is the UI context in production (WPF's
        // DispatcherSynchronizationContext, Avalonia's AvaloniaSynchronizationContext); with no
        // context (a test host with no pump) there is nothing to marshal onto and the handler runs
        // inline. Keying off the captured context, rather than a process-global dispatcher, keeps the
        // decision local and framework-neutral.
        var context = SynchronizationContext.Current;
        var subscriberThread = Environment.CurrentManagedThreadId;
        return bus.Subscribe(name, env =>
        {
            if (context is null || Environment.CurrentManagedThreadId == subscriberThread)
                handler(env);
            else
                context.Post(_ => handler(env), null);
        });
    }
}
