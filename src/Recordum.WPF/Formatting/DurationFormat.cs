using System.Globalization;

namespace Recordum.WPF.Formatting;

/// <summary>Parse/format di durate per la cella tempo (spec multivalue §4.1, scelta A: mask mm:ss).
/// Solo cifre = mm:ss compattato ("130" → 1:30). Format auto-scala: mm:ss → h:mm:ss → "Ng h:mm:ss" → "Na Ng".</summary>
public static class DurationFormat
{
    public static bool TryParse(string? text, out int seconds)
    {
        seconds = 0;
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return false;
        if (t.All(char.IsAsciiDigit))
        {
            // cifre nude: le ultime due sono i secondi ("130" → 1:30, "45" → 0:45)
            int ss = int.Parse(t.Length <= 2 ? t : t[^2..], CultureInfo.InvariantCulture);
            int mm = t.Length <= 2 ? 0 : int.Parse(t[..^2], CultureInfo.InvariantCulture);
            if (ss > 59) return false;
            seconds = mm * 60 + ss;
            return true;
        }
        var parts = t.Split(':');
        if (parts.Length is < 2 or > 3) return false;
        var nums = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i])) return false;
        if (parts.Length == 2)
        {
            if (nums[1] > 59) return false;
            seconds = nums[0] * 60 + nums[1];
        }
        else
        {
            if (nums[1] > 59 || nums[2] > 59) return false;
            seconds = nums[0] * 3600 + nums[1] * 60 + nums[2];
        }
        return true;
    }

    public static string Format(int seconds)
    {
        if (seconds < 0) seconds = 0;
        const int Day = 86400, Year = 365 * Day;
        if (seconds >= Year) return FormatDays(seconds / Day);
        if (seconds >= Day)
        {
            int d = seconds / Day, rest = seconds % Day;
            return string.Create(CultureInfo.InvariantCulture,
                $"{d}g {rest / 3600}:{rest % 3600 / 60:00}:{rest % 60:00}");
        }
        if (seconds >= 3600)
            return string.Create(CultureInfo.InvariantCulture,
                $"{seconds / 3600}:{seconds % 3600 / 60:00}:{seconds % 60:00}");
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    /// <summary>"41g", "2a 41g" — usato anche dalla streak Resistance in formato tempo-trascorso.</summary>
    public static string FormatDays(int days)
    {
        if (days < 0) days = 0;
        return days < 365
            ? string.Create(CultureInfo.InvariantCulture, $"{days}g")
            : string.Create(CultureInfo.InvariantCulture, $"{days / 365}a {days % 365}g");
    }
}
