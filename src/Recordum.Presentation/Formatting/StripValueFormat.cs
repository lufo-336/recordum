using System.Globalization;

namespace Recordum.Presentation.Formatting;

/// <summary>Il valore di un giorno scritto nella striscia della Home: al più 4 caratteri, separatore della cultura.</summary>
public static class StripValueFormat
{
    /// <summary>0,5 · 7 · 12 · 1,2k · 12k · 1,2M. Si tronca, non si arrotonda: la cella non dice mai più di quanto c'è.</summary>
    public static string Compact(decimal value, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (value < 10m && value != decimal.Truncate(value)) return Floor1(value).ToString("0.#", culture);
        if (value < 1000m) return decimal.Truncate(value).ToString("0", culture);
        return Scaled(value / 1000m, "k", culture);
    }

    /// <summary>Una durata in secondi: minuti sotto l'ora ("45m"), ore oltre ("1,5h", "12h").</summary>
    public static string Duration(decimal seconds, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (seconds < 3600m) return decimal.Truncate(seconds / 60m).ToString("0", culture) + "m";
        var hours = seconds / 3600m;
        return (hours < 10m ? Floor1(hours).ToString("0.#", culture) : decimal.Truncate(hours).ToString("0", culture)) + "h";
    }

    private static string Scaled(decimal v, string suffix, CultureInfo culture)
    {
        if (v < 10m) return Floor1(v).ToString("0.#", culture) + suffix;
        if (v < 1000m) return decimal.Truncate(v).ToString("0", culture) + suffix;
        return suffix == "k" ? Scaled(v / 1000m, "M", culture) : "999M";
    }

    private static decimal Floor1(decimal v) => decimal.Floor(v * 10m) / 10m;
}
