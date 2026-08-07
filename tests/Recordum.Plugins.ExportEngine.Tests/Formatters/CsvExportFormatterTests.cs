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
        Assert.Equal("habit,date,value,metric,unit,retroactive", lines[0].Trim());
        Assert.Contains(lines, l => l.StartsWith("Run,2026-05-20", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_names_the_unit_the_value_is_in()
    {
        // The value column stays numeric for machine consumers; the unit rides in its own column,
        // or "5" could mean kilometres in one file and hours in the next.
        var text = Encoding.UTF8.GetString(new CsvExportFormatter().Write(SampleModel.One()));
        Assert.Contains("Run,2026-05-20,5,Distance,km,false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_leaves_the_unit_column_empty_when_the_metric_has_none()
    {
        var model = SampleModel.One();
        var habit = model.Habits[0];
        var bare = habit.Completions[0] with { Unit = null };
        var text = Encoding.UTF8.GetString(new CsvExportFormatter().Write(
            model with { Habits = [habit with { Completions = [bare] }] }));

        Assert.Contains("Run,2026-05-20,5,Distance,,false", text, StringComparison.Ordinal);
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
