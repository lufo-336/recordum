using System.Text.Json.Serialization;

namespace Recordum.Plugins.Contracts.Manifest;

[JsonConverter(typeof(JsonStringEnumConverter<PluginPackageType>))]
public enum PluginPackageType { Plugin, Template, Theme }
