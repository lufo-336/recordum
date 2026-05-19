using Recordum.Core.Abstractions;

namespace Recordum.Data.Tests.Fixtures;

public sealed class FakePluginContext : IPluginContext
{
    public string? PluginId { get; set; }

    public IDisposable BeginScope(string pluginId)
    {
        var prev = PluginId;
        PluginId = pluginId;
        return new Releaser(this, prev);
    }

    private sealed class Releaser(FakePluginContext ctx, string? previous) : IDisposable
    {
        public void Dispose() => ctx.PluginId = previous;
    }
}
