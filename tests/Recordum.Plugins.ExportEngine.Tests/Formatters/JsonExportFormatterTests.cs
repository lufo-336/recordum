using System.Text;
using System.Text.Json;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class JsonExportFormatterTests
{
    [Fact]
    public void Identity_and_format()
    {
        var f = new JsonExportFormatter();
        Assert.Equal(WellKnownExtensionPoints.ExportFormat, f.PointId);
        Assert.Equal("io.recordum.export-engine", f.PluginId);
        Assert.Equal(ExportFormat.Json, f.Format);
        Assert.True(f.CanFormat(ExportFormat.Json));
        Assert.False(f.CanFormat(ExportFormat.Csv));
    }

    [Fact]
    public void Write_produces_parseable_json_with_the_title()
    {
        var bytes = new JsonExportFormatter().Write(SampleModel.One());
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        Assert.Equal("Run", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("habits").GetArrayLength());
    }
}
