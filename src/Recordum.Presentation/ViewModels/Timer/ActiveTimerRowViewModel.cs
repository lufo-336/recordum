using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Errors;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.Timer;

public sealed partial class ActiveTimerRowViewModel : ObservableObject
{
    private readonly IActiveTimerService _timers;
    private readonly IClock _clock;
    private readonly Func<Task> _onTimerGone;

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

    /// <param name="onTimerGone">Chiamato quando il timer della riga non esiste più (per esempio cancellato
    /// con l'abitudine, senza evento): chi ospita la riga si ricarica invece di lasciare una riga morta.</param>
    public ActiveTimerRowViewModel(ActiveTimer timer, string habitName, IActiveTimerService timers, IClock clock,
        Func<Task> onTimerGone)
    {
        Timer = timer;
        HabitName = habitName;
        _timers = timers;
        _clock = clock;
        _onTimerGone = onTimerGone;
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
        try
        {
            await _timers.StopAsync(HabitNodeId).ConfigureAwait(true);
        }
        catch (ActiveTimerNotFoundException)
        {
            // F2a-11: la riga era morta. Non c'è niente da fermare: la barra si ricarica.
            await _onTimerGone().ConfigureAwait(true);
        }
    }
}
