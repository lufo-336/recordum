using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Timer;

public sealed partial class TimerStatusBarViewModel : ObservableObject
{
    private readonly IActiveTimerService _timers;
    private readonly IClock _clock;
    private readonly IHabitNodeService _habits;
    private readonly ITickSource _tick;
    private int _refreshGeneration;

    public ObservableCollection<ActiveTimerRowViewModel> Rows { get; } = [];

    public bool IsVisible => Rows.Count > 0;

    public TimerStatusBarViewModel(IActiveTimerService timers, IClock clock, IHabitNodeService habits,
        IEventBus events, ITickSourceFactory ticks)
    {
        _timers = timers;
        _clock = clock;
        _habits = habits;
        // The bar ticks itself, and only while it has rows: an idle app keeps no timer alive.
        _tick = ticks.Create(TimeSpan.FromSeconds(1));
        _tick.Tick += (_, _) => Tick();
        Rows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsVisible));
            if (Rows.Count > 0) _tick.Start(); else _tick.Stop();
        };
        // A timer started, paused, resumed or stopped anywhere (the habit screen, a row of the bar) reloads
        // the bar; before this it loaded once at startup and never learned of a timer started later.
        foreach (var name in (EventName[])[WellKnownEvents.TimerStarted, WellKnownEvents.TimerPaused,
                     WellKnownEvents.TimerResumed, WellKnownEvents.TimerStopped])
            events.SubscribeOnUi(name, e => _ = RefreshInBackground());
    }

    private async Task RefreshInBackground()
    {
        try { await RefreshAsync().ConfigureAwait(true); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"TimerStatusBar refresh failed: {ex}"); }
    }

    public ValueTask LoadAsync(CancellationToken ct = default) => RefreshAsync(ct);

    public async ValueTask RefreshAsync(CancellationToken ct = default)
    {
        // Overlapping refreshes (events in quick succession) must not interleave their adds: build
        // aside, and only the newest one swaps the rows in.
        var generation = ++_refreshGeneration;
        var active = await _timers.ListActiveAsync(ct).ConfigureAwait(true);
        var rows = new List<ActiveTimerRowViewModel>();
        foreach (var t in active)
        {
            var node = await _habits.GetAsync(t.HabitNodeId, ct).ConfigureAwait(true);
            rows.Add(new ActiveTimerRowViewModel(t, node?.Name ?? "?", _timers, _clock));
        }
        if (generation != _refreshGeneration) return;
        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);
    }

    public void Tick()
    {
        foreach (var r in Rows) r.Tick();
    }
}
