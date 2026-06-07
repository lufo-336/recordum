using Microsoft.Web.WebView2.Core;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Shared test double for <see cref="IWebView2Availability"/>. Defaults to available;
/// pass <c>available: false</c> to simulate a missing WebView2 Runtime.</summary>
public sealed class FakeWebView2Availability(bool available = true) : IWebView2Availability
{
    public bool IsAvailable { get; } = available;
    public CoreWebView2Environment? Environment => null;
    public Task ProbeAsync() => Task.CompletedTask;
}
