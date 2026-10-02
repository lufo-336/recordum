using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>La card di riga ha cinque stati che in WPF erano DataTrigger su uno Style e qui sono
/// classi CSS. La prova non è uno screenshot: si legge il <c>Background</c> effettivo del Border
/// realizzato in headless.</summary>
public class RowCardTintTests
{
    private static (Border Card, HomeRowViewModel Vm) Show()
    {
        // #E11D48 e non un blu: Brush.Accent in Light è #FF3B82F6 e falsifica il test su droptarget.
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), color: "#E11D48"));
        var view = new HabitRow { DataContext = vm };
        var w = new Window { Width = 400, Height = 100, Content = view };
        w.Show();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var card = view.GetLogicalDescendants().OfType<Border>().First(b => b.Classes.Contains("habitblock"));
        return (card, vm);
    }

    [AvaloniaFact]
    public void Resting_background_is_the_habit_colour_at_alpha_26()
    {
        var (card, _) = Show();
        card.Background.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().Be(Color.FromArgb(26, 0xE1, 0x1D, 0x48));
    }

    [AvaloniaFact]
    public void Selected_background_is_alpha_51_and_border_is_the_habit_colour()
    {
        var (card, vm) = Show();
        vm.IsSelected = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        card.Background.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().Be(Color.FromArgb(51, 0xE1, 0x1D, 0x48));
        card.BorderBrush.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().Be(Color.FromArgb(255, 0xE1, 0x1D, 0x48));
        card.BorderThickness.Top.Should().Be(1.5);
    }

    [AvaloniaFact]
    public void Drop_target_wins_over_selected()
    {
        var (card, vm) = Show();
        vm.IsSelected = true;
        vm.IsDropTarget = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        card.BorderBrush.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().NotBe(Color.FromArgb(255, 0xE1, 0x1D, 0x48));
    }

    [AvaloniaFact]
    public void Dragging_fades_the_card()
    {
        var (card, vm) = Show();
        vm.IsBeingDragged = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        card.Opacity.Should().Be(0.4);
    }

    [AvaloniaFact]
    public void The_insertion_edge_draws_on_the_right_side()
    {
        var (card, vm) = Show();
        vm.DropEdge = RowDropEdge.Before;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        card.BorderThickness.Should().Be(new Thickness(0, 2, 0, 0));

        vm.DropEdge = RowDropEdge.After;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        card.BorderThickness.Should().Be(new Thickness(0, 0, 0, 2));
    }
}
