namespace Recordum.Plugins.Contracts.ExtensionPoints;

public interface IExtensionPoint
{
    ExtensionPointId PointId { get; }
    string PluginId { get; }
}
