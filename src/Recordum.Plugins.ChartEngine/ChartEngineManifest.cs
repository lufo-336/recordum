using Recordum.Plugins.Contracts.Manifest;

namespace Recordum.Plugins.ChartEngine;

public static class ChartEngineManifest
{
    public const string PluginId = "io.recordum.chart-engine";

    public static PluginManifest Create() => new()
    {
        SchemaVersion = "1.0",
        Id = PluginId,
        Name = "Chart Engine",
        Version = "1.0.0",
        Type = PluginPackageType.Plugin,
        Publisher = new ManifestPublisher { Id = "recordum", DisplayName = "Recordum" },
        MinRecordumVersion = "3.9.0",
        Entry = "in-process",
        ExtensionPoints = ["chart.renderer"],
        SupportedLocales = ["en", "it"],
    };
}
