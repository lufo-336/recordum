using System.Windows.Threading;

namespace Recordum.WPF.ViewModels.Home;

/// <summary>
/// A single 1-second ticker shared by the whole Home list: resistance rows register a refresh
/// callback and one <see cref="DispatcherTimer"/> fans out to all of them each second (spec §2 —
/// one timer for the list, not one per row). The timer is created only under a running WPF app
/// (null in headless tests, where <see cref="TickNow"/> drives the fan-out manually). A running
/// DispatcherTimer is rooted by the dispatcher, so the owner MUST <see cref="Clear"/> on reload
/// and <see cref="Dispose"/> on teardown or it keeps ticking and leaks.
/// </summary>
public sealed class HomeClockTicker : IDisposable
{
    private readonly DispatcherTimer? _timer;
    private readonly List<Action> _callbacks = new();

    public HomeClockTicker()
    {
        if (System.Windows.Application.Current is not null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;
        }
    }

    /// <summary>Registers a per-tick callback (a resistance row's clock refresh) and starts the timer.</summary>
    public void Register(Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        _callbacks.Add(tick);
        _timer?.Start();
    }

    /// <summary>Drops all callbacks and stops the timer. Called before each Home rebuild.</summary>
    public void Clear()
    {
        _callbacks.Clear();
        _timer?.Stop();
    }

    /// <summary>Invokes every registered callback once. Wired to the timer tick; also the manual
    /// entry point for headless tests. A snapshot guards against callbacks mutating the list.</summary>
    public void TickNow()
    {
        foreach (var cb in _callbacks.ToArray())
            cb();
    }

    private void OnTick(object? sender, EventArgs e) => TickNow();

    public void Dispose()
    {
        Clear();
        if (_timer is not null)
            _timer.Tick -= OnTick;
    }
}
