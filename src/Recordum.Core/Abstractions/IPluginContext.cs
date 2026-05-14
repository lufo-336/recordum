namespace Recordum.Core.Abstractions;

public interface IPluginContext
{
    string? PluginId { get; }
    IDisposable BeginScope(string pluginId);
}
