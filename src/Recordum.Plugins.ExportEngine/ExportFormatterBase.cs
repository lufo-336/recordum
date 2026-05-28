using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;
using Recordum.Plugins.Contracts.ExtensionPoints;

namespace Recordum.Plugins.ExportEngine;

public abstract class ExportFormatterBase : IExportFormatter
{
    public const string EnginePluginId = "io.recordum.export-engine";

    public ExtensionPointId PointId => WellKnownExtensionPoints.ExportFormat;
    public string PluginId => EnginePluginId;

    public abstract ExportFormat Format { get; }
    public bool CanFormat(ExportFormat format) => format == Format;
    public abstract byte[] Write(ExportModel model);
}
