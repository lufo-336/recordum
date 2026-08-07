using System.Globalization;
using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class CsvExportFormatter : ExportFormatterBase
{
    public override ExportFormat Format => ExportFormat.Csv;

    public override byte[] Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var sb = new StringBuilder();
        // The value stays a bare number and the unit gets its own column: a consumer that wants to
        // compute on the values must not have to strip "hours" off them first.
        sb.Append("habit,date,value,metric,unit,retroactive\r\n");
        foreach (var habit in model.Habits)
        {
            foreach (var c in habit.Completions)
            {
                sb.Append(Q(habit.Name)).Append(',')
                  .Append(c.CompletedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.Value?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                  .Append(Q(c.Metric ?? "")).Append(',')
                  .Append(Q(c.Unit ?? "")).Append(',')
                  .Append(c.Retroactive ? "true" : "false").Append("\r\n");
            }
        }

        var withPeriods = model.Habits.Where(h => h.Periods.Count > 0).ToList();
        if (withPeriods.Count > 0)
        {
            sb.Append("\r\nhabit,started,ended,duration,outcome\r\n");
            foreach (var habit in withPeriods)
                foreach (var p in habit.Periods)
                    sb.Append(Q(habit.Name)).Append(',')
                      .Append(p.StartedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(',')
                      .Append(p.EndedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "").Append(',')
                      .Append(FormatPeriodDuration(p.Duration)).Append(',')
                      .Append(p.IsOpen ? "open" : "relapsed").Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Q(string s)
        => s.Contains(',', StringComparison.Ordinal) || s.Contains('"', StringComparison.Ordinal) || s.Contains('\n', StringComparison.Ordinal)
            ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : s;
}
