using System.Windows;
using System.Windows.Media;

namespace Recordum.WPF.Controls;

/// <summary>
/// Detects the effective dark/light theme for the LiveCharts2-backed detail controls,
/// which cannot consume the themed <c>DynamicResource</c> brushes directly. Reads the
/// luminance of the current <c>Brush.Background</c> resource. Re-evaluated on each redraw,
/// so a live theme swap is reflected the next time a control rebuilds.
/// </summary>
internal static class ThemeProbe
{
    public static bool IsDark()
    {
        if (Application.Current?.TryFindResource("Brush.Background") is SolidColorBrush b)
        {
            var c = b.Color;
            var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance < 0.5;
        }
        return false;
    }
}
