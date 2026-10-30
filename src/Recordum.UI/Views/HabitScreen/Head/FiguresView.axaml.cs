using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Recordum.UI.Views.HabitScreen.Head;

/// <summary>I numeri della testa, uguali per un'abitudine e per un gruppo: cambiano solo gli elementi e il colore.</summary>
public partial class FiguresView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<FiguresView, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<string?> HabitColorProperty =
        AvaloniaProperty.Register<FiguresView, string?>(nameof(HabitColor));

    public IEnumerable? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string? HabitColor { get => GetValue(HabitColorProperty); set => SetValue(HabitColorProperty, value); }

    public FiguresView() => InitializeComponent();

    /// <summary>Quante colonne di numeri (4 largo, 2 stretto).</summary>
    public int Columns
    {
        set { if (Figures.ItemsPanelRoot is UniformGrid grid) grid.Columns = value; }
    }
}
