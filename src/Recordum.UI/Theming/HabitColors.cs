using Avalonia.Media;

namespace Recordum.UI.Theming;

/// <summary>Il calcolo dei colori della direzione visiva (§4.2–4.3), in un posto solo.</summary>
public static class HabitColors
{
    /// <summary>Rapporto di contrasto WCAG 2 fra due colori opachi (1 … 21).</summary>
    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Luminanza relativa sRGB (WCAG 2).</summary>
    private static double Luminance(Color c) => 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    private static double Linear(byte channel)
    {
        var v = channel / 255d;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
