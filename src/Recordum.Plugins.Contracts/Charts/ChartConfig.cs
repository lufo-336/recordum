using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Charts;

public sealed record ChartConfig(
    [property: JsonPropertyName("range")] string Range = "90d",
    [property: JsonPropertyName("from")] string? FromIso = null,
    [property: JsonPropertyName("to")] string? ToIso = null,
    [property: JsonPropertyName("bucket")] string Bucket = "daily",
    [property: JsonPropertyName("show_legend")] bool ShowLegend = true,
    [property: JsonPropertyName("x_axis_label")] string? XAxisLabel = null,
    [property: JsonPropertyName("y_axis_label")] string? YAxisLabel = null)
{
    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static ChartConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return new ChartConfig();
        try { return JsonSerializer.Deserialize<ChartConfig>(json, Options) ?? new ChartConfig(); }
        catch (JsonException) { return new ChartConfig(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
