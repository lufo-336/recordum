using System.Collections.Concurrent;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Plugins.ExtensionPoints;

public sealed class ExtensionPointRegistry : IExtensionPointRegistry
{
    private readonly ConcurrentDictionary<ExtensionPointId, List<ExtensionRegistration>> _byPoint = new();
    private readonly object _lock = new();

    public void Register(ExtensionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (_lock)
        {
            var list = _byPoint.GetOrAdd(registration.PointId, _ => []);
            list.Add(registration);
        }
    }

    public void UnregisterAll(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        lock (_lock)
        {
            foreach (var pair in _byPoint)
            {
                pair.Value.RemoveAll(r => r.PluginId == pluginId);
            }
        }
    }

    public IReadOnlyList<IExtensionPoint> GetExtensions(ExtensionPointId pointId)
    {
        lock (_lock)
        {
            if (!_byPoint.TryGetValue(pointId, out var list)) return [];
            return list.Select(r => r.Extension).ToList();
        }
    }
}
