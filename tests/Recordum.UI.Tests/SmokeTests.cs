using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.Presentation.Resources;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;

namespace Recordum.UI.Tests;

public class SmokeTests
{
    [AvaloniaFact]
    public void The_main_window_opens_with_the_application_title()
    {
        // Non piu' `new MainWindow()`: la finestra vera vuole il suo ViewModel, e il titolo viene
        // dalle risorse invece che da una costante nello XAML.
        var window = new MainWindow(ShellFixture.Shared.NewMainWindowViewModel());
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        window.Title.Should().Be(Strings.AppTitle);
        window.IsVisible.Should().BeTrue();
    }
}
