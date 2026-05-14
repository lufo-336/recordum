using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Manifest;

[JsonConverter(typeof(JsonStringEnumConverter<AiAccess>))]
public enum AiAccess { None, Local }
