namespace Recordum.Plugins.Contracts.Permissions;

public sealed record PermissionRequest(string PluginId, PermissionId Permission, string? TargetHost = null);
