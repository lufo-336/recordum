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

    [Fact]
    public void Write_renders_periods_table_for_resistance_habit()
    {
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(SampleModel.WithAbstinencePeriods()));
        Assert.Contains("| Started | Ended | Duration | Outcome |", md, StringComparison.Ordinal);
        Assert.Contains("| 2026-06-13 09:00 | 2026-07-01 19:00 | 18.10:00:00 | relapsed |", md, StringComparison.Ordinal);
        Assert.Contains("| 2026-07-02 08:00 |  | 0.04:30:00 | open |", md, StringComparison.Ordinal);
    }
}
