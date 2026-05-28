using System.Text;
using System.Text.Json;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class JsonExportFormatter : ExportFormatterBase
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public override ExportFormat Format => ExportFormat.Json;

    public override byte[] Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model, Options));
    }
}
