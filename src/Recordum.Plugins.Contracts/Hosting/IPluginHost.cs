using Recordum.Plugins.Contracts.Manifest;

namespace Recordum.Plugins.Contracts.Hosting;

public interface IPluginHost
{
    string HostKind { get; }
    Task LoadAsync(PluginManifest manifest, CancellationToken ct = default);
    Task UnloadAsync(string pluginId, CancellationToken ct = default);
    bool IsLoaded(string pluginId);
}
