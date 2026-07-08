using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class CsvExportFormatterTests
{
    [Fact]
    public void Write_has_header_and_one_row_per_completion()
    {
        var f = new CsvExportFormatter();
        Assert.Equal(ExportFormat.Csv, f.Format);
        var text = Encoding.UTF8.GetString(f.Write(SampleModel.One()));
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("habit,date,value,metric,retroactive", lines[0].Trim());
        Assert.Contains(lines, l => l.StartsWith("Run,2026-05-20", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_quotes_fields_containing_commas()
    {
        var model = SampleModel.One() with
        {
            Habits = [SampleModel.One().Habits[0] with { Name = "Run, fast" }],
        };
        var text = Encoding.UTF8.GetString(new CsvExportFormatter().Write(model));
        Assert.Contains("\"Run, fast\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_renders_periods_table_for_resistance_habit()
    {
        var text = Encoding.UTF8.GetString(new CsvExportFormatter().Write(SampleModel.WithAbstinencePeriods()));
        Assert.Contains("habit,started,ended,duration,outcome", text, StringComparison.Ordinal);
        Assert.Contains("Quit smoking,2026-06-13 09:00,2026-07-01 19:00,18.10:00:00,relapsed", text, StringComparison.Ordinal);
        Assert.Contains("Quit smoking,2026-07-02 08:00,,0.04:30:00,open", text, StringComparison.Ordinal);
    }
}
