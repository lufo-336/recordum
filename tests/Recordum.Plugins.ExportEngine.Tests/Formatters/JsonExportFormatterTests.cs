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

    [Fact]
    public void Write_renders_periods_array_for_resistance_habit()
    {
        var bytes = new JsonExportFormatter().Write(SampleModel.WithAbstinencePeriods());
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        var periods = doc.RootElement.GetProperty("habits")[0].GetProperty("periods");
        Assert.Equal(2, periods.GetArrayLength());
        Assert.Contains(periods.EnumerateArray(), p => p.GetProperty("open").GetBoolean());

        var closed = periods.EnumerateArray().First(p => !p.GetProperty("open").GetBoolean());
        Assert.Equal("2026-06-13T09:00:00+00:00", closed.GetProperty("started_at").GetString());
        Assert.Equal("2026-07-01T19:00:00+00:00", closed.GetProperty("ended_at").GetString());
        Assert.Equal((18 * 86400) + (10 * 3600), closed.GetProperty("duration_seconds").GetInt64());

        var open = periods.EnumerateArray().First(p => p.GetProperty("open").GetBoolean());
        Assert.Equal(JsonValueKind.Null, open.GetProperty("ended_at").ValueKind);
        Assert.Equal(4 * 3600 + 30 * 60, open.GetProperty("duration_seconds").GetInt64());
    }

    [Fact]
    public void Write_includes_attachments_array_but_never_the_absolute_path()
    {
        var bytes = new JsonExportFormatter().Write(SampleModel.WithNoteAttachments(@"C:\Users\someone\AppData\Local\Recordum\attachments\note1\lake.jpg"));
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        var attachments = doc.RootElement.GetProperty("habits")[0].GetProperty("notes")[0].GetProperty("attachments");
        Assert.Equal(2, attachments.GetArrayLength());

        var lake = attachments.EnumerateArray().First(a => a.GetProperty("filename").GetString() == "lake.jpg");
        Assert.Equal("image/jpeg", lake.GetProperty("mime_type").GetString());
        Assert.Equal("attachments/note1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg", lake.GetProperty("relative_path").GetString());

        // AbsolutePath must never leak into the exported JSON (local filesystem layout / username).
        Assert.False(lake.TryGetProperty("absolute_path", out _));
        Assert.False(lake.TryGetProperty("absolutePath", out _));
        Assert.DoesNotContain("someone", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }
}
