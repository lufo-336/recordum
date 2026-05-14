using Recordum.Core.Abstractions;
using Xunit;

namespace Recordum.Core.Tests.Abstractions;

public class AmbientPluginContextTests
{
    [Fact]
    public void PluginId_is_null_outside_scope()
    {
        var ctx = new AmbientPluginContext();
        Assert.Null(ctx.PluginId);
    }

    [Fact]
    public void PluginId_is_set_inside_scope_and_cleared_after()
    {
        var ctx = new AmbientPluginContext();

        using (ctx.BeginScope("io.recordum.chart-engine"))
        {
            Assert.Equal("io.recordum.chart-engine", ctx.PluginId);
        }

        Assert.Null(ctx.PluginId);
    }

    [Fact]
    public void Nested_scopes_restore_outer_value_on_dispose()
    {
        var ctx = new AmbientPluginContext();

        using (ctx.BeginScope("outer"))
        {
            using (ctx.BeginScope("inner"))
            {
                Assert.Equal("inner", ctx.PluginId);
            }
            Assert.Equal("outer", ctx.PluginId);
        }

        Assert.Null(ctx.PluginId);
    }

    [Fact]
    public async Task PluginId_flows_across_async_boundaries()
    {
        var ctx = new AmbientPluginContext();

        string? captured = null;
        using (ctx.BeginScope("io.recordum.test"))
        {
            await Task.Yield();
            captured = ctx.PluginId;
        }

        Assert.Equal("io.recordum.test", captured);
    }
}
