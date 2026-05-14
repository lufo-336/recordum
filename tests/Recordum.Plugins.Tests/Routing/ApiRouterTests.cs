using Recordum.Plugins.Routing;
using Xunit;

namespace Recordum.Plugins.Tests.Routing;

public class ApiRouterTests
{
    [Fact]
    public async Task RegisterHandler_then_CallAsync_invokes_handler_with_passed_args()
    {
        var router = new ApiRouter();
        string? receivedPlugin = null;
        object? receivedParams = null;
        var expectedResult = new object();

        router.RegisterHandler("habit.list", (pluginId, parameters, ct) =>
        {
            receivedPlugin = pluginId;
            receivedParams = parameters;
            return Task.FromResult<object?>(expectedResult);
        });

        var payload = new { filter = "active" };
        var result = await router.CallAsync("io.recordum.test", "habit.list", payload);

        Assert.Equal("io.recordum.test", receivedPlugin);
        Assert.Same(payload, receivedParams);
        Assert.Same(expectedResult, result);
    }

    [Fact]
    public async Task CallAsync_to_unknown_method_throws_InvalidOperationException()
    {
        var router = new ApiRouter();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.CallAsync("io.recordum.test", "nonexistent.method", null));
    }

    [Fact]
    public async Task Registering_same_method_twice_replaces_previous_handler()
    {
        var router = new ApiRouter();
        router.RegisterHandler("my.method", (_, _, _) => Task.FromResult<object?>("first"));
        router.RegisterHandler("my.method", (_, _, _) => Task.FromResult<object?>("second"));

        var result = await router.CallAsync("plugin", "my.method", null);

        Assert.Equal("second", result);
    }

    [Fact]
    public void RegisterHandler_null_method_throws_ArgumentNullException()
    {
        var router = new ApiRouter();
        Assert.Throws<ArgumentNullException>(() => router.RegisterHandler(null!, (_, _, _) => Task.FromResult<object?>(null)));
    }

    [Fact]
    public void RegisterHandler_whitespace_method_throws_ArgumentException()
    {
        var router = new ApiRouter();
        Assert.Throws<ArgumentException>(() => router.RegisterHandler("  ", (_, _, _) => Task.FromResult<object?>(null)));
    }

    [Fact]
    public void RegisterHandler_null_handler_throws_ArgumentNullException()
    {
        var router = new ApiRouter();
        Assert.Throws<ArgumentNullException>(() => router.RegisterHandler("some.method", null!));
    }

    [Fact]
    public async Task CallAsync_null_pluginId_throws_ArgumentNullException()
    {
        var router = new ApiRouter();
        await Assert.ThrowsAsync<ArgumentNullException>(() => router.CallAsync(null!, "some.method", null));
    }

    [Fact]
    public async Task CallAsync_whitespace_method_throws_ArgumentException()
    {
        var router = new ApiRouter();
        await Assert.ThrowsAsync<ArgumentException>(() => router.CallAsync("plugin", "  ", null));
    }
}
