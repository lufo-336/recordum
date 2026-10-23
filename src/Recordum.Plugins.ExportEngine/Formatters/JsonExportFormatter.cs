using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class JsonExportFormatter : ExportFormatterBase
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new PeriodRowConverter() },
    };

    public override ExportFormat Format => ExportFormat.Json;

    public override ExportOutput Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return ExportOutput.Single(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model, Options)));
    }

    /// <summary>
    /// Shapes each <see cref="ExportPeriodRow"/> as { started_at, ended_at, duration_seconds, open } —
    /// a deliberately different field set from the record's own property names (IsOpen -> open,
    /// Duration -> duration_seconds as whole seconds), so it needs its own converter rather than
    /// the automatic reflection-based mapping the rest of the model relies on.
    /// </summary>
    private sealed class PeriodRowConverter : JsonConverter<ExportPeriodRow>
    {
        public override ExportPeriodRow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("Export JSON is write-only.");

        public override void Write(Utf8JsonWriter writer, ExportPeriodRow value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("started_at", value.StartedAt);
            if (value.EndedAt is { } endedAt)
                writer.WriteString("ended_at", endedAt);
            else
                writer.WriteNull("ended_at");
            writer.WriteNumber("duration_seconds", (long)value.Duration.TotalSeconds);
            writer.WriteBoolean("open", value.IsOpen);
            writer.WriteEndObject();
        }
    }
}
