using FluentAssertions;
using Recordum.WPF;
using Xunit;

namespace Recordum.WPF.Tests;

public class MainWindowLayoutTests
{
    [Fact]
    public void ClampToWorkArea_keeps_size_when_it_fits()
    {
        var (w, h) = MainWindow.ClampToWorkArea(1280, 760, 1920, 1040);
        w.Should().Be(1280);
        h.Should().Be(760);
    }

    [Fact]
    public void ClampToWorkArea_shrinks_height_when_taller_than_work_area()
    {
        // Regression: default 1280x760 on a 1280x672 work area (1280x720 screen
        // minus taskbar) opened taller than the screen, pushing the bottom Save
        // buttons / TimerStatusBar off-screen.
        var (w, h) = MainWindow.ClampToWorkArea(1280, 760, 1280, 672);
        w.Should().Be(1280);
        h.Should().Be(672);
    }

    [Fact]
    public void ClampToWorkArea_shrinks_both_dimensions_on_a_small_screen()
    {
        var (w, h) = MainWindow.ClampToWorkArea(1280, 760, 1024, 600);
        w.Should().Be(1024);
        h.Should().Be(600);
    }
}
