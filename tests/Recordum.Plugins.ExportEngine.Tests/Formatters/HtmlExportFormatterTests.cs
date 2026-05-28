using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class HtmlExportFormatterTests
{
    [Fact]
    public void Write_is_self_contained_html_with_title_and_embedded_heatmap()
    {
        var f = new HtmlExportFormatter();
        Assert.Equal(ExportFormat.Html, f.Format);
        var html = Encoding.UTF8.GetString(f.Write(SampleModel.One()));
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Run</title>", html, StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
        Assert.Contains("felt good", html, StringComparison.Ordinal);
    }
}
