using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Base for the QuickAdd VMs that record multi-field series rows and share the per-cell
/// stopwatch (Measured / CounterSeries). Holds the chrono state and <c>ToggleChrono</c> so the two
/// don't duplicate it (CR21).</summary>
public abstract partial class SeriesQuickAddViewModelBase : QuickAddViewModelBase
{
    private readonly IActiveTimerService _timers;
    private SeriesCellViewModel? _chronoCell;

    [ObservableProperty] private bool _chronoRunning;

    protected SeriesQuickAddViewModelBase(ICompletionService completions, IClock clock, IActiveTimerService timers)
        : base(completions, clock)
    {
        ArgumentNullException.ThrowIfNull(timers);
        _timers = timers;
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
            await _timers.StartAsync(Node.Id, TimerMode.Stopwatch, ct).ConfigureAwait(true);
            _chronoCell = cell;
            ChronoRunning = true;
        }
        else
        {
            var secs = await _timers.StopWithoutRecordingAsync(Node.Id, ct).ConfigureAwait(true);
            (_chronoCell ?? cell).SetDurationSeconds(secs);
            _chronoCell = null;
            ChronoRunning = false;
        }
    }
}
