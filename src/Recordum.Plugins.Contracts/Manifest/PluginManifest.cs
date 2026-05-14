using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Manifest;

public sealed record PluginManifest
{
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("type")]
    public PluginPackageType Type { get; init; } = PluginPackageType.Plugin;

    [JsonPropertyName("publisher")]
    public required ManifestPublisher Publisher { get; init; }

    [JsonPropertyName("min_recordum_version")]
    public required string MinRecordumVersion { get; init; }

    [JsonPropertyName("description_key")]
    public string? DescriptionKey { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("entry")]
    public required string Entry { get; init; }

    [JsonPropertyName("permissions")]
    public ManifestPermissions Permissions { get; init; } = new();

    [JsonPropertyName("extension_points")]
    public IReadOnlyList<string> ExtensionPoints { get; init; } = [];

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; init; }

    [JsonPropertyName("supported_locales")]
    public IReadOnlyList<string> SupportedLocales { get; init; } = [];
}
