using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Plugins.ChartEngine;

public sealed class ChartEngineActivator(IPluginManager manager, IExtensionPointRegistry registry) : IChartEngineActivator
{
    public async Task ActivateAsync(CancellationToken ct = default)
    {
        var manifest = ChartEngineManifest.Create();
        await manager.EnableAsync(manifest, ct).ConfigureAwait(false);
        var renderer = new ChartEngineRenderer();
        registry.Register(new ExtensionRegistration(manifest.Id, WellKnownExtensionPoints.ChartRenderer, renderer));
    }
}
