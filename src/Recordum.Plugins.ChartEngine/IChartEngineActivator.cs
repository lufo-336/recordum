namespace Recordum.Plugins.ChartEngine;

public interface IChartEngineActivator
{
    Task ActivateAsync(CancellationToken ct = default);
}
