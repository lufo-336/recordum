using Recordum.Plugins.Contracts.Manifest;

namespace Recordum.Plugins.ExportEngine;

public static class ExportEngineManifest
{
    public const string PluginId = "io.recordum.export-engine";

    public static PluginManifest Create() => new()
    {
        SchemaVersion = "1.0",
        Id = PluginId,
        Name = "Export Engine",
        Version = "1.0.0",
        Type = PluginPackageType.Plugin,
        Publisher = new ManifestPublisher { Id = "recordum", DisplayName = "Recordum" },
        MinRecordumVersion = "3.10.0",
        Entry = "in-process",
        ExtensionPoints = ["export.format"],
        SupportedLocales = ["en", "it"],
    };
}
