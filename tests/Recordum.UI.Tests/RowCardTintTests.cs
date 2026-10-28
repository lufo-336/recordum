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
/// classi CSS. La tinta (direzione visiva §4.3) è un livello col colore pieno e l'opacità del token tematico
/// <c>Opacity.RowTint</c> sopra il pannello: si leggono il fondo della card e il livello realizzati in headless.</summary>
public class RowCardTintTests
{
    private static (Border Card, HomeRowViewModel Vm) Show() => Show(out _);

    private static (Border Card, HomeRowViewModel Vm) Show(out Window w)
    {
        // #E11D48: lontano dall'inchiostro di Brush.Accent, che il droptarget usa per il bordo.
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), color: "#E11D48"));
        var view = new HabitRow { DataContext = vm };
        w = new Window { Width = 400, Height = 100, Content = view, RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light };
        w.Show();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var card = view.GetLogicalDescendants().OfType<Border>().First(b => b.Classes.Contains("habitblock"));
        return (card, vm);
    }

    private static Border Tint(Border card) => card.GetLogicalDescendants().OfType<Border>().First(b => b.Classes.Contains("tint"));

    [AvaloniaFact]
    public void Resting_tint_is_the_habit_colour_at_the_row_tint_opacity_over_the_pane()
    {
        var (card, _) = Show();
        card.Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(Color.Parse("#FFFFFF"), "Brush.Pane");
        Tint(card).Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(Color.Parse("#E11D48"));
        Tint(card).Opacity.Should().BeApproximately(0.09, 0.001);
    }

    [AvaloniaFact]
    public void The_row_tint_follows_a_theme_change_at_runtime()
    {
        // Review Focus 1, sulla riga vera: in scuro la tinta è più forte (0.16) sopra il pannello scuro, senza ricostruire.
        var (card, _) = Show(out var w);
        w.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Tint(card).Opacity.Should().BeApproximately(0.16, 0.001);
        card.Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(Color.Parse("#222221"));
    }

    [AvaloniaFact]
    public void Selected_tint_is_stronger_and_border_is_the_habit_colour()
    {
        var (card, vm) = Show();
        vm.IsSelected = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Tint(card).Opacity.Should().BeApproximately(0.16, 0.001);
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
