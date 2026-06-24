using System.Threading;

namespace Recordum.WPF.Services;

/// <summary>Records the latest picked colour into the recents MRU only after a short quiet period, so
/// dragging the HSV square or typing a hex digit-by-digit doesn't flood the MRU with transient
/// intermediate colours — only the settled choice is kept (CR23).</summary>
public sealed class DebouncedColorRecorder(IRecentColorStore store, int delayMs = 600)
{
    private CancellationTokenSource? _cts;

    /// <summary>Schedules <paramref name="hex"/> to be added to the recents after the quiet period,
    /// cancelling any colour scheduled but not yet recorded.</summary>
    public void Record(string hex)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = DelayThenAddAsync(hex, cts.Token);
    }

    private async Task DelayThenAddAsync(string hex, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct).ConfigureAwait(false);
            await store.AddAsync(hex, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* superseded by a newer colour before it settled */ }
    }
}
