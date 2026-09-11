using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Presentation.Formatting;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>A live-ticking elapsed clock: given a start instant, exposes a FormatStopwatch string
/// refreshed every second. Reusable by the quick-add, the detail streak summary, and the open
/// period row. The tick source is created only when a factory is supplied (null in headless tests).
/// A running tick source is rooted by the dispatcher, so owners MUST Stop()/Dispose() the clock
/// when discarded (on reload or teardown) or it keeps ticking and leaks.</summary>
public sealed partial class LiveElapsedClock : ObservableObject, IDisposable
{
    private readonly IClock _clock;
    private readonly ITickSource? _tick;
    private long? _startedMs;

    [ObservableProperty] private string _display = string.Empty;

    /// <summary><paramref name="ticks"/> null = l'orologio mostra il valore al momento di Start()
    /// e non si aggiorna più da solo (test headless, anteprime inerti).</summary>
    public LiveElapsedClock(IClock clock, ITickSourceFactory? ticks)
    {
        _clock = clock;
        _tick = ticks?.Create(TimeSpan.FromSeconds(1));
        if (_tick is not null)
            _tick.Tick += OnTick;
    }

    private void OnTick(object? sender, EventArgs e) => Refresh();

    public void Start(long startedAtMs)
    {
        _startedMs = startedAtMs;
        Refresh();
        _tick?.Start();
    }

    public void Stop()
    {
        _tick?.Stop();
        _startedMs = null;
        Display = string.Empty;
    }

    /// <summary>Ferma la sorgente e ne stacca l'handler, così l'orologio (e il suo proprietario) può essere raccolto.</summary>
    public void Dispose()
    {
        if (_tick is not null)
        {
            _tick.Stop();
            _tick.Tick -= OnTick;
            _tick.Dispose();
        }
        _startedMs = null;
        Display = string.Empty;
    }

    private void Refresh()
        => Display = _startedMs is { } s
            ? DurationFormat.FormatStopwatch(_clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(s))
            : string.Empty;
}
