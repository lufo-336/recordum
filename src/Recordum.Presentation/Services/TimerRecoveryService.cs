using Recordum.Presentation.ViewModels.Timer;

namespace Recordum.Presentation.Services;

public sealed class TimerRecoveryService(TimerStatusBarViewModel statusBar)
{
    public Task RecoverAsync(CancellationToken ct = default)
        => statusBar.LoadAsync(ct).AsTask();
}
