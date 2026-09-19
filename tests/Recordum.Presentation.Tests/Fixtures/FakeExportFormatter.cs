using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;
using Recordum.Plugins.Contracts.ExtensionPoints;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeExportFormatter(ExportFormat format) : IExportFormatter
{
    private readonly ExportFormat _format = format;

    public bool WriteCalled { get; private set; }
    public ExtensionPointId PointId => WellKnownExtensionPoints.ExportFormat;
    public string PluginId => "test.export";
    public ExportFormat Format => _format;
    public bool CanFormat(ExportFormat format) => format == _format;
    public byte[] Write(ExportModel model) { WriteCalled = true; return [1, 2, 3]; }
}
