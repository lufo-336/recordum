using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.UI.Views.HabitScreen.Group;

/// <summary>La colonna Panoramica del gruppo. Un clic su una riga (non sui suoi pulsanti) apre la figlia; il menu della riga
/// non c'è qui (lo dà la Home, <c>RowMenu</c> lo sa).</summary>
public partial class PanoramicaColumnView : UserControl
{
    public PanoramicaColumnView()
    {
        InitializeComponent();
        AddHandler(PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnTogetherToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Together.Expanded = !Together.Expanded;

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || DataContext is not HabitScreenViewModel vm) return;
        HomeRowViewModel? row = null;
        for (var v = e.Source as Avalonia.Visual; v is not null; v = v.GetVisualParent())
        {
            if (v is TextBox or Button or MenuItem) return;
            if (row is null && v is StyledElement { DataContext: HomeRowViewModel r }) row = r;
        }
        if (row is not null) vm.Panoramica.SelectRowCommand.Execute(row);
    }
}
