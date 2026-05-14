namespace Recordum.Plugins.Contracts.ExtensionPoints;

public sealed record ExtensionRegistration(
    string PluginId,
    ExtensionPointId PointId,
    IExtensionPoint Extension);
