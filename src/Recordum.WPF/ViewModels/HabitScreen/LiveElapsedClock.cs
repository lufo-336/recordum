using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.WPF.Formatting;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>A live-ticking elapsed clock: given a start instant, exposes a FormatStopwatch string
/// refreshed every second. Reusable by the quick-add, the detail streak summary, and the open
/// period row. The DispatcherTimer is created only under a running WPF app (null in headless tests).</summary>
public sealed partial class LiveElapsedClock : ObservableObject
{
    private readonly IClock _clock;
    private readonly DispatcherTimer? _timer;
    private long? _startedMs;

    [ObservableProperty] private string _display = string.Empty;

    public LiveElapsedClock(IClock clock)
    {
        _clock = clock;
        if (System.Windows.Application.Current is not null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Refresh();
        }
    }

    public void Start(long startedAtMs)
    {
        _startedMs = startedAtMs;
        Refresh();
        _timer?.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _startedMs = null;
        Display = string.Empty;
    }

    private void Refresh()
        => Display = _startedMs is { } s
            ? DurationFormat.FormatStopwatch(_clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(s))
            : string.Empty;
}
