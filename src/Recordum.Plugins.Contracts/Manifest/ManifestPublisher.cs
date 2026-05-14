using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Manifest;

public sealed record ManifestPublisher
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("display_name")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
