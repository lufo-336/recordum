using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Recordum.UI.Services;
using Xunit;

namespace Recordum.UI.Tests.Services;

public class AvaloniaApplicationLifetimeTests
{
    [AvaloniaFact]
    public void Shutdown_calls_Shutdown_on_the_injected_lifetime()
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime();
        var exited = false;
        lifetime.Exit += (_, _) => exited = true;
        var sut = new AvaloniaApplicationLifetime(lifetime);

        sut.Shutdown();

        exited.Should().BeTrue();
    }
}
