using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Recordum.WPF.Views;

public partial class HeatmapStrip : UserControl
{
    public static readonly DependencyProperty CountsProperty =
        DependencyProperty.Register(nameof(Counts), typeof(ImmutableArray<int>), typeof(HeatmapStrip),
            new PropertyMetadata(default(ImmutableArray<int>), OnChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(string), typeof(HeatmapStrip),
            new PropertyMetadata("#3B82F6", OnChanged));

    public ImmutableArray<int> Counts
    {
        get => (ImmutableArray<int>)GetValue(CountsProperty);
        set => SetValue(CountsProperty, value);
    }

    public string AccentColor
    {
        get => (string)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public HeatmapStrip()
    {
        InitializeComponent();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HeatmapStrip)d).Rebuild();

    private void Rebuild()
    {
        if (Counts.IsDefault || Counts.Length == 0)
        {
            Cells.ItemsSource = null;
            return;
        }

        var color = (Color)ColorConverter.ConvertFromString(AccentColor);
        var items = new List<object>(Counts.Length);
        for (var i = 0; i < Counts.Length; i++)
        {
            var step = Counts[i] switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                3 => 3,
                _ => 4,
            };
            var alpha = step switch
            {
                0 => (byte)32,
                1 => (byte)96,
                2 => (byte)160,
                3 => (byte)208,
                _ => (byte)255,
            };
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            items.Add(new { Brush = brush, BorderThickness = i == Counts.Length - 1 ? new Thickness(1) : new Thickness(0) });
        }
        Cells.ItemsSource = items;
    }
}
