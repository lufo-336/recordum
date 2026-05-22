using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.Timer;

public sealed partial class ActiveTimerRowViewModel : ObservableObject
{
    private readonly IActiveTimerService _timers;
    private readonly IClock _clock;

    public ActiveTimer Timer { get; }
    public Guid HabitNodeId => Timer.HabitNodeId;
    public string HabitName { get; }

    [ObservableProperty] private int _elapsedSeconds;
    [ObservableProperty] private bool _isPaused;

    public string ElapsedDisplay
    {
        get
        {
            var ts = TimeSpan.FromSeconds(ElapsedSeconds);
            return ElapsedSeconds < 3600
                ? ts.ToString(@"mm\:ss", CultureInfo.InvariantCulture)
                : ts.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }
    }

    public ActiveTimerRowViewModel(ActiveTimer timer, string habitName, IActiveTimerService timers, IClock clock)
    {
        Timer = timer;
        HabitName = habitName;
        _timers = timers;
        _clock = clock;
        IsPaused = timer.PausedAtMs is not null;
        Tick();
    }

    public void Tick()
    {
        var endMs = Timer.PausedAtMs ?? _clock.UtcNowMs;
        var ms = endMs - Timer.StartedAtMs - Timer.AccumulatedPausedMs;
        if (ms < 0) ms = 0;
        ElapsedSeconds = (int)(ms / 1000); // partial OnElapsedSecondsChanged raises ElapsedDisplay.
    }

    partial void OnElapsedSecondsChanged(int value) => OnPropertyChanged(nameof(ElapsedDisplay));

    [RelayCommand]
    private async Task PauseAsync()
    {
        await _timers.PauseAsync(HabitNodeId).ConfigureAwait(true);
        Timer.PausedAtMs = _clock.UtcNowMs;
        IsPaused = true;
    }

    [RelayCommand]
    private async Task ResumeAsync()
    {
        await _timers.ResumeAsync(HabitNodeId).ConfigureAwait(true);
        if (Timer.PausedAtMs is { } pAt)
            Timer.AccumulatedPausedMs += _clock.UtcNowMs - pAt;
        Timer.PausedAtMs = null;
        IsPaused = false;
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _timers.StopAsync(HabitNodeId).ConfigureAwait(true);
    }
}
