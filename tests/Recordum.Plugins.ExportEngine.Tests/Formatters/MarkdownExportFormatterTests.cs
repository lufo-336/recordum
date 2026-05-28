using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class MarkdownExportFormatterTests
{
    [Fact]
    public void Write_contains_title_summary_and_completion_table()
    {
        var f = new MarkdownExportFormatter();
        Assert.Equal(ExportFormat.Markdown, f.Format);
        var md = Encoding.UTF8.GetString(f.Write(SampleModel.One()));
        Assert.Contains("# Run", md, StringComparison.Ordinal);
        Assert.Contains("Current streak", md, StringComparison.Ordinal);
        Assert.Contains("| Date | Value | Metric |", md, StringComparison.Ordinal);
        Assert.Contains("felt good", md, StringComparison.Ordinal);
    }
}
