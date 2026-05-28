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
        sb.Append("habit,date,value,metric,retroactive\r\n");
        foreach (var habit in model.Habits)
        {
            foreach (var c in habit.Completions)
            {
                sb.Append(Q(habit.Name)).Append(',')
                  .Append(c.CompletedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.Value?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                  .Append(Q(c.Metric ?? "")).Append(',')
                  .Append(c.Retroactive ? "true" : "false").Append("\r\n");
            }
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Q(string s)
        => s.Contains(',', StringComparison.Ordinal) || s.Contains('"', StringComparison.Ordinal) || s.Contains('\n', StringComparison.Ordinal)
            ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : s;
}
