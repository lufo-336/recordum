using Avalonia.Media;

namespace Recordum.UI.Theming;

/// <summary>Il calcolo dei colori della direzione visiva (§4.2–4.3), in un posto solo.</summary>
public static class HabitColors
{
    /// <summary>Il colore <paramref name="top"/> steso con <paramref name="opacity"/> (0…1) sopra <paramref name="under"/>:
    /// è anche la tinta di una riga, perché il livello di tinta è il colore pieno con un'opacità sul pannello.</summary>
    public static Color Blend(Color top, double opacity, Color under)
    {
        var a = Math.Clamp(opacity, 0d, 1d);
        byte Mix(byte t, byte u) => (byte)Math.Round(t * a + u * (1 - a));
        return Color.FromRgb(Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

    /// <summary>La variante "testo" del colore di un'abitudine: in chiaro più scura, in scuro più chiara, quanto basta per
    /// 4,5:1 sulla tinta del colore stesso (<paramref name="tintOpacity"/> sul <paramref name="pane"/>).</summary>
    public static Color TextVariant(Color habit, Color pane, double tintOpacity, bool dark)
    {
        // In chiaro si scurisce verso il nero, in scuro si schiarisce verso il bianco partendo da 70 % colore e 30 %
        // bianco (come Loop): a passi del 4 %, fino al primo colore che fa 4,5:1 sulla tinta.
        var bg = Blend(habit, tintOpacity, pane);
        var target = dark ? Colors.White : Colors.Black;
        var start = dark ? Blend(Colors.White, 0.30, habit) : habit;
        for (var step = 0; step <= MaxSteps; step++)
        {
            var c = Blend(target, step * StepSize, start);
            if (Contrast(c, bg) >= MinTextContrast) return c;
        }
        return target;
    }

    private const double MinTextContrast = 4.5;
    private const double StepSize = 0.04;
    private const int MaxSteps = 25;

    /// <summary>Un colore d'abitudine "#RRGGBB" (o "#AARRGGBB", alfa ignorato) letto senza eccezioni.</summary>
    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#' || (hex.Length != 7 && hex.Length != 9)) return false;
        if (!Color.TryParse(hex, out var parsed)) return false;
        color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
        return true;
    }

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
