using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Plugins.ChartEngine;

// Dal 2026-11-24 il plugin non registra piu' un IChartRenderer: il renderer era WPF (spenta).
// Il renderer Avalonia arrivera' con la sua onda; per ora il plugin si attiva e basta.
public sealed class ChartEngineActivator(IPluginManager manager) : IChartEngineActivator
{
    public async Task ActivateAsync(CancellationToken ct = default)
    {
        // Idempotent: ApplicationInitializer.InitializeAsync may run more than once.
        if (manager.ListActivatedManifests().Any(m => m.Id == ChartEngineManifest.PluginId)) return;

        await manager.EnableAsync(ChartEngineManifest.Create(), ct).ConfigureAwait(false);
    }
}
