using Avalonia.Headless.XUnit;
using FluentAssertions;
using Recordum.UI.Views;

namespace Recordum.UI.Tests;

public class SmokeTests
{
    [AvaloniaFact]
    public void The_main_window_opens_with_a_title()
    {
        var window = new MainWindow();
        window.Show();

        window.Title.Should().Be("Recordum");
        window.IsVisible.Should().BeTrue();
    }
}
