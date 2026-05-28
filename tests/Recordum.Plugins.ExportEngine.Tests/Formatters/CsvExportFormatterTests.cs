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
}
