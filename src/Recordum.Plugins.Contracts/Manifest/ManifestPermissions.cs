using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Manifest;

public sealed record ManifestPermissions
{
    [JsonPropertyName("data")]
    public IReadOnlyList<string> Data { get; init; } = [];

    [JsonPropertyName("network")]
    public IReadOnlyList<string> Network { get; init; } = [];

    [JsonPropertyName("notifications")]
    public bool Notifications { get; init; }

    [JsonPropertyName("filesystem")]
    public FilesystemAccess Filesystem { get; init; } = FilesystemAccess.None;

    [JsonPropertyName("ai")]
    public AiAccess Ai { get; init; } = AiAccess.None;
}
