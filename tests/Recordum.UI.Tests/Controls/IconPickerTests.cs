using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.Services;
using Recordum.UI.Controls;
using Xunit;

namespace Recordum.UI.Tests.Controls;

public class IconPickerTests
{
    private static Control PanelOf(IconPicker picker)
    {
        var trigger = picker.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null);
        return (Control)((Flyout)trigger.Flyout!).Content!;
    }

    private static List<Button> Cells(Control panel) =>
        panel.GetLogicalDescendants().OfType<Button>().Where(b => b.Tag is "icon").ToList();

    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void Picking_an_icon_writes_its_key_and_no_icon_writes_null()
    {
        var picker = new IconPicker { Icon = null };
        var w = new Window { Content = picker };
        w.Show();
        try
        {
            var panel = PanelOf(picker);
            Click(Cells(panel).First(b => AutomationProperties.GetName(b) == "Heart"));
            picker.Icon.Should().Be("Icon.Heart");

            Click(panel.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "NoIcon"));
            picker.Icon.Should().BeNull();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_grid_offers_the_habit_icons_only_each_with_a_name_and_search_filters_it()
    {
        var picker = new IconPicker();
        var w = new Window { Content = picker };
        w.Show();
        try
        {
            var panel = PanelOf(picker);
            Cells(panel).Should().HaveCount(IconCatalog.All.Count);
            Cells(panel).Should().OnlyContain(b => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)));
            panel.GetLogicalDescendants().OfType<Button>().Should().OnlyContain(b => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)));

            panel.GetLogicalDescendants().OfType<TextBox>().Single().Text = "heart";
            Cells(panel).Select(AutomationProperties.GetName).Should().BeEquivalentTo("Heart", "HeartPulse");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void The_selected_icon_is_marked()
    {
        var picker = new IconPicker { Icon = "Icon.Moon" };
        var w = new Window { Content = picker };
        w.Show();
        try
        {
            Cells(PanelOf(picker)).Where(b => b.Classes.Contains("selected"))
                .Select(AutomationProperties.GetName).Should().Equal("Moon");
        }
        finally { w.Close(); }
    }
}
