namespace Recordum.Plugins.Contracts.Hosting;

public interface IApiRouter
{
    Task<object?> CallAsync(string pluginId, string method, object? parameters, CancellationToken ct = default);
}
