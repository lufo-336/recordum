using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Recordum.Core.HomeRow;

namespace Recordum.WPF.Views;

/// <summary>
/// The Home-row strip. Two input modes:
/// <list type="bullet">
/// <item><see cref="Fractions"/> + <see cref="Kind"/> (2.1-bis): per-type visual — filled/empty
/// squares (Action), value-height mini-bars (Counter), continuous band with tears (Resistance),
/// dots (Journal), value ramp cells (State). The strip keeps the SAME total width whatever the
/// day count (7 or 14 → narrower cells), so rows align (A13b).</item>
/// <item><see cref="Counts"/> only (legacy, Panoramica child rows): equal cells tinted by the
/// count intensity ramp.</item>
/// </list>
/// </summary>
public partial class HeatmapStrip : UserControl
{
    // One shared total width so every row's strip occupies the same footprint (A13b).
    private const double TotalWidth = 96;
    private const double TrackHeight = 16;

    public static readonly DependencyProperty CountsProperty =
        DependencyProperty.Register(nameof(Counts), typeof(ImmutableArray<int>), typeof(HeatmapStrip),
            new PropertyMetadata(default(ImmutableArray<int>), OnChanged));

    public static readonly DependencyProperty FractionsProperty =
        DependencyProperty.Register(nameof(Fractions), typeof(ImmutableArray<double>), typeof(HeatmapStrip),
            new PropertyMetadata(default(ImmutableArray<double>), OnChanged));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(StripStyle), typeof(HeatmapStrip),
            new PropertyMetadata(StripStyle.Squares, OnChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(string), typeof(HeatmapStrip),
            new PropertyMetadata("#3B82F6", OnChanged));

    public ImmutableArray<int> Counts
    {
        get => (ImmutableArray<int>)GetValue(CountsProperty);
        set => SetValue(CountsProperty, value);
    }

    public ImmutableArray<double> Fractions
    {
        get => (ImmutableArray<double>)GetValue(FractionsProperty);
        set => SetValue(FractionsProperty, value);
    }

    public StripStyle Kind
    {
        get => (StripStyle)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
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

    private sealed record Cell(double Width, double Height, Thickness Margin, CornerRadius Radius,
        VerticalAlignment VAlign, Brush Brush);

    private void Rebuild()
    {
        if (!Fractions.IsDefaultOrEmpty)
        {
            Cells.ItemsSource = BuildStyledCells();
            return;
        }
        if (Counts.IsDefault || Counts.Length == 0)
        {
            Cells.ItemsSource = null;
            return;
        }
        Cells.ItemsSource = BuildLegacyCells();
    }

    private SolidColorBrush Accent(double alpha)
    {
        var color = (Color)ColorConverter.ConvertFromString(AccentColor);
        var brush = new SolidColorBrush(Color.FromArgb((byte)alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private List<Cell> BuildStyledCells()
    {
        var f = Fractions;
        var n = f.Length;
        var slot = TotalWidth / n;
        var gap = Kind == StripStyle.Band ? 0d : Math.Min(2d, slot * 0.2);
        var w = slot - gap;
        var cells = new List<Cell>(n);

        for (var i = 0; i < n; i++)
        {
            var v = f[i];
            var on = v > 0d;
            var margin = new Thickness(0, 0, gap, 0);
            Cell cell = Kind switch
            {
                // Counter: bottom-anchored bars, height follows the day's value vs the period max.
                StripStyle.Bars => new Cell(
                    w, on ? Math.Max(3d, v * TrackHeight) : 2d, margin, new CornerRadius(1),
                    VerticalAlignment.Bottom, Accent(on ? 255 : 48)),

                // Journal: a dot on days with an entry, a faint pin-point otherwise.
                StripStyle.Dots => new Cell(
                    on ? 6d : 3d, on ? 6d : 3d,
                    new Thickness((slot - (on ? 6d : 3d)) / 2, 0, (slot - (on ? 6d : 3d)) / 2, 0),
                    new CornerRadius((on ? 6d : 3d) / 2), VerticalAlignment.Center, Accent(on ? 255 : 56)),

                // Resistance: joined ribbon over covered days; uncovered days are the faint track,
                // so relapses read as tears in an otherwise continuous band.
                StripStyle.Band => new Cell(
                    slot, 8d, default, BandRadius(f, i),
                    VerticalAlignment.Center, Accent(on ? 230 : 24)),

                // State: full cells tinted by the value ramp; no-value days stay faint.
                StripStyle.Ramp => new Cell(
                    w, 12d, margin, new CornerRadius(2),
                    VerticalAlignment.Center, Accent(on ? 96 + v * 159 : 32)),

                // Action (and fallback): filled vs empty squares.
                _ => new Cell(
                    w, Math.Min(w, 10d), margin, new CornerRadius(2),
                    VerticalAlignment.Center, Accent(on ? 255 : 32)),
            };
            cells.Add(cell);
        }
        return cells;
    }

    // Round only the outer edges of each covered run, so consecutive days fuse into one band.
    private static CornerRadius BandRadius(ImmutableArray<double> f, int i)
    {
        if (f[i] <= 0d) return default;
        var startOfRun = i == 0 || f[i - 1] <= 0d;
        var endOfRun = i == f.Length - 1 || f[i + 1] <= 0d;
        return new CornerRadius(startOfRun ? 4 : 0, endOfRun ? 4 : 0, endOfRun ? 4 : 0, startOfRun ? 4 : 0);
    }

    private List<Cell> BuildLegacyCells()
    {
        var cells = new List<Cell>(Counts.Length);
        foreach (var count in Counts)
        {
            var step = count switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => 4 };
            var alpha = step switch { 0 => 32d, 1 => 96d, 2 => 160d, 3 => 208d, _ => 255d };
            cells.Add(new Cell(10, 10, new Thickness(0, 0, 2, 0), new CornerRadius(2),
                VerticalAlignment.Center, Accent(alpha)));
        }
        return cells;
    }
}
