using Recordum.Plugins.Contracts.ExtensionPoints;

namespace Recordum.Plugins.Contracts.Hosting;

public interface IExtensionPointRegistry
{
    void Register(ExtensionRegistration registration);
    void UnregisterAll(string pluginId);
    IReadOnlyList<IExtensionPoint> GetExtensions(ExtensionPointId pointId);
}
