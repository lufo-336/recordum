using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Export;

public sealed record ExportLayout(
    [property: JsonPropertyName("metadata")] bool Metadata = true,
    [property: JsonPropertyName("summary")] bool Summary = true,
    [property: JsonPropertyName("heatmap")] bool Heatmap = true,
    [property: JsonPropertyName("completions")] bool Completions = true,
    [property: JsonPropertyName("notes")] bool Notes = true,
    [property: JsonPropertyName("range")] string Range = "all")
{
    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static ExportLayout Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return new ExportLayout();
        try { return JsonSerializer.Deserialize<ExportLayout>(json, Options) ?? new ExportLayout(); }
        catch (JsonException) { return new ExportLayout(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
