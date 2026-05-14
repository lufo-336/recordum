using System.Collections.Concurrent;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Plugins.Routing;

public sealed class ApiRouter : IApiRouter
{
    private readonly ConcurrentDictionary<string, Func<string, object?, CancellationToken, Task<object?>>> _handlers = new();

    public void RegisterHandler(string method, Func<string, object?, CancellationToken, Task<object?>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[method] = handler;
    }

    public Task<object?> CallAsync(string pluginId, string method, object? parameters, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (!_handlers.TryGetValue(method, out var handler))
        {
            throw new InvalidOperationException($"No handler registered for method '{method}'");
        }
        return handler(pluginId, parameters, ct);
    }
}
