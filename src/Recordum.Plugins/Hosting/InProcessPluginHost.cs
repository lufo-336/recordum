using System.Collections.Concurrent;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;

namespace Recordum.Plugins.Hosting;

public sealed class InProcessPluginHost : IPluginHost
{
    private readonly ConcurrentDictionary<string, PluginManifest> _loaded = new();

    public string HostKind => "in-process";

    public Task LoadAsync(PluginManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!_loaded.TryAdd(manifest.Id, manifest))
        {
            throw new InvalidOperationException($"Plugin '{manifest.Id}' is already loaded");
        }
        return Task.CompletedTask;
    }

    public Task UnloadAsync(string pluginId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        _loaded.TryRemove(pluginId, out _);
        return Task.CompletedTask;
    }

    public bool IsLoaded(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return _loaded.ContainsKey(pluginId);
    }
}
