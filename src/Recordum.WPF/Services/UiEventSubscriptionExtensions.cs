using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.WPF.Services;

/// <summary>
/// Subscribes to <see cref="IEventBus"/> events with marshaling onto the WPF UI dispatcher.
/// </summary>
/// <remarks>
/// <see cref="EventBus"/> invokes handlers synchronously on the publisher's thread, which is often a
/// thread-pool thread (completion/note services record with <c>ConfigureAwait(false)</c>). View-model
/// handlers that touch view-bound <c>ObservableCollection</c>s would then mutate them off the UI thread
/// and crash with a cross-thread <see cref="System.NotSupportedException"/>. Marshaling at the
/// subscription boundary keeps the platform-agnostic bus (reused by web/MAUI) free of WPF concerns.
/// See phase-4 issue B-uiThread.
/// </remarks>
internal static class UiEventSubscriptionExtensions
{
    public static IDisposable SubscribeOnUi(this IEventBus bus, EventName name, Action<EventEnvelope> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        // Capture the dispatcher of the thread that subscribes. View models are constructed on the UI
        // thread, so this is the UI dispatcher in production. Keying off the captured dispatcher (rather
        // than the process-global Application.Current.Dispatcher) keeps the decision local and avoids
        // marshaling onto a foreign/non-pumping dispatcher leaked by other code (e.g. in tests).
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        return bus.Subscribe(name, env =>
        {
            if (dispatcher.CheckAccess())
                handler(env);
            else
                dispatcher.InvokeAsync(() => handler(env));
        });
    }
}
