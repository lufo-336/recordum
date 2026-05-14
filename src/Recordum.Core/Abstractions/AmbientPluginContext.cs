namespace Recordum.Core.Abstractions;

public sealed class AmbientPluginContext : IPluginContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public string? PluginId => Current.Value;

    public IDisposable BeginScope(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var previous = Current.Value;
        Current.Value = pluginId;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public Scope(string? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            Current.Value = _previous;
            _disposed = true;
        }
    }
}
