using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAssertions;
using Xunit;

namespace Recordum.UI.Tests.Styles;

public class ControlStylesScreenshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Gallery(string variant)
    {
        var parts = ControlGallery.Build();
        var window = new Window
        {
            Width = 900, Height = 620,
            RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = parts.Root,
        };
        window.Show();
        window.TryGetResource("Brush.Background", window.ActualThemeVariant, out var bg);
        window.Background = (Avalonia.Media.IBrush)bg!;
        Dispatcher.UIThread.RunJobs();

        parts.Combo.IsDropDownOpen = true;
        parts.Menu.Open(parts.MenuHost);
        ToolTip.SetIsOpen(parts.TipHost, true);
        Dispatcher.UIThread.RunJobs();

        var path = ScreenshotProbeTests.Capture(window, $"gallery-{variant}");
        new FileInfo(path).Length.Should().BeGreaterThan(1000);
    }
}
