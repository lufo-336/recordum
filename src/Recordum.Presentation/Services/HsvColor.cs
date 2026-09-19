using System.Globalization;

namespace Recordum.Presentation.Services;

/// <summary>Pure HSV ↔ #RRGGBB conversion for the visual color picker. H in [0,360), S/V in [0,1].</summary>
public static class HsvColor
{
    /// <summary>Parses "#RRGGBB" to HSV. Returns null for malformed input.</summary>
    public static (double H, double S, double V)? FromHex(string? hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#') return null;
        if (!byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return null;
        return RgbToHsv(r, g, b);
    }

    public static string ToHex(double h, double s, double v)
    {
        var (r, g, b) = HsvToRgb(h, s, v);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double d = max - min;

        double h = 0;
        if (d > 0)
        {
            if (max == rd) h = 60 * (((gd - bd) / d) % 6);
            else if (max == gd) h = 60 * (((bd - rd) / d) + 2);
            else h = 60 * (((rd - gd) / d) + 4);
            if (h < 0) h += 360;
        }
        double s = max == 0 ? 0 : d / max;
        return (h, s, max);
    }

    public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360; // normalize
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        (double r1, double g1, double b1) = ((int)(h / 60)) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return (Round(r1 + m), Round(g1 + m), Round(b1 + m));
    }

    private static byte Round(double channel) => (byte)Math.Clamp(Math.Round(channel * 255), 0, 255);
}
