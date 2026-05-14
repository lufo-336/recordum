using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExtensionPoints;
using Xunit;

namespace Recordum.Plugins.Tests.ExtensionPoints;

public class ExtensionPointRegistryTests
{
    private static readonly ExtensionPointId PointA = new("chart.renderer");
    private static readonly ExtensionPointId PointB = new("export.block");

    private sealed class FakeExtension(ExtensionPointId pointId, string pluginId) : IExtensionPoint
    {
        public ExtensionPointId PointId => pointId;
        public string PluginId => pluginId;
    }

    [Fact]
    public void Register_then_GetExtensions_returns_the_registered_extension()
    {
        var registry = new ExtensionPointRegistry();
        var ext = new FakeExtension(PointA, "io.recordum.test");
        registry.Register(new ExtensionRegistration("io.recordum.test", PointA, ext));

        var result = registry.GetExtensions(PointA);

        Assert.Single(result);
        Assert.Same(ext, result[0]);
    }

    [Fact]
    public void Register_multiple_for_same_point_preserves_insertion_order()
    {
        var registry = new ExtensionPointRegistry();
        var ext1 = new FakeExtension(PointA, "plugin.one");
        var ext2 = new FakeExtension(PointA, "plugin.two");
        var ext3 = new FakeExtension(PointA, "plugin.three");

        registry.Register(new ExtensionRegistration("plugin.one", PointA, ext1));
        registry.Register(new ExtensionRegistration("plugin.two", PointA, ext2));
        registry.Register(new ExtensionRegistration("plugin.three", PointA, ext3));

        var result = registry.GetExtensions(PointA);

        Assert.Equal(3, result.Count);
        Assert.Same(ext1, result[0]);
        Assert.Same(ext2, result[1]);
        Assert.Same(ext3, result[2]);
    }

    [Fact]
    public void UnregisterAll_removes_only_the_matching_plugin_extensions()
    {
        var registry = new ExtensionPointRegistry();
        var extA = new FakeExtension(PointA, "plugin.target");
        var extB = new FakeExtension(PointA, "plugin.other");
        var extC = new FakeExtension(PointB, "plugin.target");

        registry.Register(new ExtensionRegistration("plugin.target", PointA, extA));
        registry.Register(new ExtensionRegistration("plugin.other", PointA, extB));
        registry.Register(new ExtensionRegistration("plugin.target", PointB, extC));

        registry.UnregisterAll("plugin.target");

        var resultA = registry.GetExtensions(PointA);
        var resultB = registry.GetExtensions(PointB);

        Assert.Single(resultA);
        Assert.Same(extB, resultA[0]);
        Assert.Empty(resultB);
    }

    [Fact]
    public void GetExtensions_on_empty_point_returns_empty_list()
    {
        var registry = new ExtensionPointRegistry();

        var result = registry.GetExtensions(new ExtensionPointId("nonexistent.point"));

        Assert.Empty(result);
    }

    [Fact]
    public void Register_null_throws_ArgumentNullException()
    {
        var registry = new ExtensionPointRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void UnregisterAll_null_pluginId_throws_ArgumentNullException()
    {
        var registry = new ExtensionPointRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.UnregisterAll(null!));
    }

    [Fact]
    public void UnregisterAll_whitespace_pluginId_throws_ArgumentException()
    {
        var registry = new ExtensionPointRegistry();
        Assert.Throws<ArgumentException>(() => registry.UnregisterAll("   "));
    }
}
