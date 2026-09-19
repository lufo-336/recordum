using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Events;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests;

public class UiEventSubscriptionExtensionsTests
{
    /// <summary>Un contesto che registra ogni Post e li esegue solo quando lo si svuota — come un
    /// dispatcher UI: il lavoro postato gira "sul lato subscriber", non sul thread del publisher.</summary>
    private sealed class RecordingContext : SynchronizationContext
    {
        public List<(SendOrPostCallback Callback, object? State)> Posted { get; } = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (Posted) Posted.Add((d, state));
        }

        public int Drain()
        {
            (SendOrPostCallback Callback, object? State)[] items;
            lock (Posted)
            {
                items = [.. Posted];
                Posted.Clear();
            }
            foreach (var (callback, state) in items) callback(state);
            return items.Length;
        }
    }

    /// <summary>Runs <paramref name="work"/> on a dedicated thread with no synchronization context and
    /// joins it. Not <c>Task.Run(...).Wait()</c>: Wait may inline the task on the waiting thread, which
    /// would make "the other thread" the subscribing thread and the test vacuous.</summary>
    private static T OnOtherThread<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { error = ex; }
        });
        thread.Start();
        thread.Join();
        if (error is not null) throw new InvalidOperationException("Publisher thread failed.", error);
        return result;
    }

    private static void PublishCompletion(EventBus bus) =>
        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));

    [Fact]
    public void SubscribeOnUi_publish_from_another_thread_is_posted_to_the_subscribers_context_not_run_inline()
    {
        var previous = SynchronizationContext.Current;
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var bus = new EventBus();
            var subscriberThread = Environment.CurrentManagedThreadId;
            var handlerRuns = new List<int>();
            using var sub = bus.SubscribeOnUi(WellKnownEvents.CompletionRecorded,
                _ => handlerRuns.Add(Environment.CurrentManagedThreadId));

            // Publish from a dedicated thread with no context of its own (an await would post its
            // continuation to the RecordingContext, which only runs on Drain()).
            OnOtherThread(() =>
            {
                PublishCompletion(bus);
                return 0;
            });

            Assert.Empty(handlerRuns);                 // not run inline on the publisher's thread
            Assert.Single(context.Posted);             // it arrived through Post

            Assert.Equal(1, context.Drain());          // the subscriber-side context runs it
            Assert.Equal([subscriberThread], handlerRuns);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void SubscribeOnUi_publish_on_the_subscribing_thread_runs_inline_without_posting()
    {
        var previous = SynchronizationContext.Current;
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var bus = new EventBus();
            var runs = 0;
            using var sub = bus.SubscribeOnUi(WellKnownEvents.CompletionRecorded, _ => runs++);

            PublishCompletion(bus);

            Assert.Equal(1, runs);
            Assert.Empty(context.Posted);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Documenta il ramo "nessun contesto" (un host di test senza pump): non c'è dove
    /// marshallare, quindi l'handler gira in linea sul thread del publisher. È voluto, e con la
    /// vecchia implementazione a Dispatcher non era raggiungibile.</summary>
    [Fact]
    public void SubscribeOnUi_with_no_context_at_subscription_runs_the_handler_inline_on_the_publisher_thread()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var bus = new EventBus();
            var handlerThread = -1;
            using var sub = bus.SubscribeOnUi(WellKnownEvents.CompletionRecorded,
                _ => handlerThread = Environment.CurrentManagedThreadId);

            var publisherThread = OnOtherThread(() =>
            {
                PublishCompletion(bus);
                return Environment.CurrentManagedThreadId;
            });

            Assert.NotEqual(Environment.CurrentManagedThreadId, publisherThread);
            Assert.Equal(publisherThread, handlerThread);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
