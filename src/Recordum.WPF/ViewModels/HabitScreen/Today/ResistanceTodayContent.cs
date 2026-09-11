using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>Resistance "Oggi" content (spec §1, the flagship): the big live abstinence clock plus
/// Inizio / Ricaduta and an attempts-in-90-days caption. Mirrors
/// <see cref="QuickAdd.AbstinenceQuickAddViewModel"/>'s Start/Lapse/clock logic; owns a
/// <see cref="LiveElapsedClock"/> that must be disposed on reload/teardown.</summary>
public sealed partial class ResistanceTodayContent : ObservableObject, ITodayContent, ITodayContentChanges, IDisposable
{
    private readonly IAbstinenceService _abstinence;
    private readonly IClock _clock;
    private Guid _habitId;

    /// <summary>The live-ticking clean-streak clock (bound as the strip's large display).</summary>
    public LiveElapsedClock Clock { get; }

    [ObservableProperty] private AbstinenceStatus _status = AbstinenceStatus.NotStarted;
    [ObservableProperty] private int _attemptsLast90Days;

    public bool CanStart => Status != AbstinenceStatus.Abstaining;
    public bool CanLapse => Status == AbstinenceStatus.Abstaining;

    /// <summary>Raised after a successful Start/Lapse so the host reloads the columns that read
    /// abstinence state (Detail streak, calendar markers, records, quick-add).</summary>
    public event EventHandler? Changed;

    public ResistanceTodayContent(IAbstinenceService abstinence, IClock clock, ITickSourceFactory? ticks = null)
    {
        ArgumentNullException.ThrowIfNull(abstinence);
        ArgumentNullException.ThrowIfNull(clock);
        _abstinence = abstinence;
        _clock = clock;
        Clock = new LiveElapsedClock(clock, ticks);
    }

    private DayBoundary _day = DayBoundary.Utc;

    public async Task LoadAsync(HabitNode node, DayBoundary day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        _habitId = node.Id;
        _day = day;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    private async Task ReloadAsync(CancellationToken ct = default)
    {
        var state = await _abstinence.GetStateAsync(_habitId, ct).ConfigureAwait(true);
        Status = state.Status;
        if (state.Status == AbstinenceStatus.Abstaining && state.CurrentStartedAtMs is { } startedMs)
            Clock.Start(startedMs);
        else
            Clock.Stop();

        var today = _day.Today(_clock);
        var from = today.AddDays(-90);
        var periods = await _abstinence.ListPeriodsAsync(_habitId, from, today, ct).ConfigureAwait(true);
        var fromMs = _day.RangeOf(from).From.ToUnixTimeMilliseconds();
        AttemptsLast90Days = periods.Count(p => p.StartedAtMs >= fromMs);
    }

    partial void OnStatusChanged(AbstinenceStatus value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanLapse));
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        await _abstinence.StartAsync(_habitId).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task LapseAsync()
    {
        await _abstinence.LapseAsync(_habitId).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Clock.Dispose();
}
