using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Base for the QuickAdd VMs that record multi-field series rows and share the per-cell
/// stopwatch (Measured / CounterSeries). Holds the chrono state and <c>ToggleChrono</c> so the two
/// don't duplicate it (CR21).</summary>
public abstract partial class SeriesQuickAddViewModelBase : QuickAddViewModelBase, IDisposable
{
    private readonly IActiveTimerService _timers;
    private SeriesCellViewModel? _chronoCell;

    [ObservableProperty] private bool _chronoRunning;

    /// <summary>Live-ticking elapsed readout shown next to the running duration cell (U8). One timer
    /// per habit, so one shared clock. MUST be disposed with the VM (the tick source leaks otherwise).</summary>
    public LiveElapsedClock ChronoClock { get; }

    protected SeriesQuickAddViewModelBase(ICompletionService completions, IClock clock, IActiveTimerService timers, ITickSourceFactory? ticks = null)
        : base(completions, clock)
    {
        ArgumentNullException.ThrowIfNull(timers);
        _timers = timers;
        ChronoClock = new LiveElapsedClock(Clock, ticks);
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
            var secs = await _timers.StopWithoutRecordingAsync(Node.Id, ct).ConfigureAwait(true);
            var target = _chronoCell ?? cell;
            target.SetDurationSeconds(secs);
            target.IsChronoRunning = false;
            ChronoClock.Stop();
            _chronoCell = null;
            ChronoRunning = false;
        }
    }

    /// <summary>Stops any running chrono and detaches the clock's tick source so the VM can be
    /// collected when the screen swaps or tears down the quick-add.</summary>
    public void Dispose()
    {
        if (_chronoCell is not null) _chronoCell.IsChronoRunning = false;
        _chronoCell = null;
        ChronoRunning = false;
        ChronoClock.Dispose();
        GC.SuppressFinalize(this);
    }
}
