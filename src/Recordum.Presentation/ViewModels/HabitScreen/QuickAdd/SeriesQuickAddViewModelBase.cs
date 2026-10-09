using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Base for the QuickAdd VMs that record multi-field series rows and share the per-cell
/// stopwatch (Measured / CounterSeries). Holds the chrono state and <c>ToggleChrono</c> so the two
/// don't duplicate it (CR21).</summary>
public abstract partial class SeriesQuickAddViewModelBase : QuickAddViewModelBase, IDisposable
{
    private readonly IActiveTimerService _timers;
    private SeriesCellViewModel? _chronoCell;
    private readonly List<IDisposable> _subs = [];

    [ObservableProperty] private bool _chronoRunning;

    /// <summary>Live-ticking elapsed readout shown next to the running duration cell (U8). One timer
    /// per habit, so one shared clock. MUST be disposed with the VM (the tick source leaks otherwise).</summary>
    public LiveElapsedClock ChronoClock { get; }

    protected SeriesQuickAddViewModelBase(ICompletionService completions, IClock clock, IActiveTimerService timers, ITickSourceFactory? ticks = null, IEventBus? events = null)
        : base(completions, clock)
    {
        ArgumentNullException.ThrowIfNull(timers);
        _timers = timers;
        ChronoClock = new LiveElapsedClock(Clock, ticks);
        // The per-cell stopwatch is an active_timer row, so it can be stopped elsewhere (the status bar):
        // without this the cell stays "running" and the next press throws ActiveTimerNotFoundException.
        if (events is null) return;
        foreach (var name in (EventName[])[WellKnownEvents.TimerStarted, WellKnownEvents.TimerPaused,
                     WellKnownEvents.TimerResumed, WellKnownEvents.TimerStopped])
            _subs.Add(events.SubscribeOnUi(name, e => OnTimerEvent(e)));
    }

    private void OnTimerEvent(EventEnvelope e)
    {
        if (Node is null || e.Payload is not TimerChangedPayload p || p.HabitNodeId != Node.Id) return;
        _ = ResyncChronoAsync();
    }

    private async Task ResyncChronoAsync()
    {
        try
        {
            if (Node is null || !ChronoRunning) return;
            if (await _timers.GetActiveAsync(Node.Id).ConfigureAwait(true) is null) ResetChrono();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ResyncChronoAsync failed: {ex}"); }
    }

    private void ResetChrono()
    {
        if (_chronoCell is not null) _chronoCell.IsChronoRunning = false;
        _chronoCell = null;
        ChronoClock.Stop();
        ChronoRunning = false;
    }

    /// <summary>Per-cell stopwatch: start runs an active_timer (Stopwatch); stop pours the elapsed
    /// seconds into the duration cell WITHOUT recording a completion (spec multivalue §4.1, §8).
    /// One active timer per habit (UNIQUE on active_timer): if the habit's own timer is already
    /// running, the per-cell stopwatch does not start.</summary>
    [RelayCommand]
    private async Task ToggleChrono(SeriesCellViewModel? cell, CancellationToken ct)
    {
        if (Node is null || cell is null || !cell.IsDuration) return;
        if (!ChronoRunning)
        {
            if (await _timers.GetActiveAsync(Node.Id, ct).ConfigureAwait(true) is not null) return;
            var timer = await _timers.StartAsync(Node.Id, TimerMode.Stopwatch, ct).ConfigureAwait(true);
            _chronoCell = cell;
            cell.IsChronoRunning = true;
            ChronoClock.Start(timer.StartedAtMs);
            ChronoRunning = true;
        }
        else
        {
            int secs;
            try { secs = await _timers.StopWithoutRecordingAsync(Node.Id, ct).ConfigureAwait(true); }
            catch (ActiveTimerNotFoundException)
            {
                // Stopped elsewhere (the status bar records it): nothing to pour in, just clear the cell.
                ResetChrono();
                return;
            }
            var target = _chronoCell ?? cell;
            target.SetDurationSeconds(secs);
            ResetChrono();
        }
    }

    /// <summary>Stops any running chrono and detaches the clock's tick source so the VM can be
    /// collected when the screen swaps or tears down the quick-add.</summary>
    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
        if (_chronoCell is not null) _chronoCell.IsChronoRunning = false;
        _chronoCell = null;
        ChronoRunning = false;
        ChronoClock.Dispose();
        GC.SuppressFinalize(this);
    }
}
