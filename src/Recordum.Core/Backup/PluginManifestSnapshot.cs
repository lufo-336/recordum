namespace Recordum.Core.Backup;

public sealed record PluginManifestSnapshot(string PluginId, string Version, bool Enabled, bool Sideloaded);
