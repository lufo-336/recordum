using Recordum.Plugins.Contracts.Manifest;

namespace Recordum.Plugins.Contracts.Hosting;

public interface IPluginManager
{
    Task EnableAsync(PluginManifest manifest, CancellationToken ct = default);
    Task DisableAsync(string pluginId, CancellationToken ct = default);
    IReadOnlyCollection<string> ListEnabled();
    IReadOnlyList<PluginManifest> ListActivatedManifests();
}
