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

    [Fact]
    public void Write_renders_periods_table_for_resistance_habit()
    {
        var html = Encoding.UTF8.GetString(new HtmlExportFormatter().Write(SampleModel.WithAbstinencePeriods()));
        Assert.Contains("<th>Started</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Ended</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Duration</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th>Outcome</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>2026-06-13 09:00</td><td>2026-07-01 19:00</td><td>18.10:00:00</td><td>relapsed</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>2026-07-02 08:00</td><td></td><td>0.04:30:00</td><td>open</td>", html, StringComparison.Ordinal);
    }
}
