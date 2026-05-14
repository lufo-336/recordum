using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Manifest;

[JsonConverter(typeof(JsonStringEnumConverter<FilesystemAccess>))]
public enum FilesystemAccess
{
    None,
    PluginStorage,
    AttachmentsRead,
    UserPick
}
