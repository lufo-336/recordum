using Recordum.WPF.ViewModels.Timer;

namespace Recordum.WPF.Services;

public sealed class TimerRecoveryService(TimerStatusBarViewModel statusBar)
{
    public Task RecoverAsync(CancellationToken ct = default)
        => statusBar.LoadAsync(ct).AsTask();
}
