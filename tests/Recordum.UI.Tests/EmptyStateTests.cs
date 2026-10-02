using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using ShapePath = Avalonia.Controls.Shapes.Path;   // `Path` da solo è ambiguo con System.IO.Path
using Xunit;

namespace Recordum.UI.Tests;

public class EmptyStateTests
{
    private static (EmptyState View, HomeViewModel Vm) Show()
    {
        var (vm, _, _) = HomeFixture.Build();
        var view = new EmptyState { DataContext = vm };
        var w = new Window { Width = 500, Height = 400, Content = view };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, vm);
    }

    [AvaloniaFact]
    public void The_active_tab_shows_the_seedling_and_the_call_to_action()
    {
        var (view, _) = Show();
        var text = view.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("emptytext"));
        var cta = view.GetLogicalDescendants().OfType<Button>().Single();
        text.Text.Should().Be(Strings.Home_Empty_Active);
        cta.IsVisible.Should().BeTrue();
        view.GetLogicalDescendants().OfType<ShapePath>().Single().Data.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void Archived_and_trash_change_the_line_and_hide_the_call_to_action()
    {
        var (view, vm) = Show();
        var text = view.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("emptytext"));
        var cta = view.GetLogicalDescendants().OfType<Button>().Single();
        var icon = view.GetLogicalDescendants().OfType<ShapePath>().Single();
        var activeGeometry = icon.Data;

        vm.ActiveTab = HomeTab.Archived;
        Dispatcher.UIThread.RunJobs();
        text.Text.Should().Be(Strings.Home_Empty_Archived);
        cta.IsVisible.Should().BeFalse();
        icon.Data.Should().NotBeSameAs(activeGeometry);

        vm.ActiveTab = HomeTab.Trash;
        Dispatcher.UIThread.RunJobs();
        text.Text.Should().Be(Strings.Home_Empty_Trash);
        cta.IsVisible.Should().BeFalse();
    }
}
