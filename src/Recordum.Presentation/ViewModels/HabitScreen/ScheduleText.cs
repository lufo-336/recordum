using System.Globalization;
using System.Text.Json;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>Il programma in una riga, sotto il nome nella testa ("Ogni giorno", "lun, mer, ven").</summary>
public static class ScheduleText
{
    private static readonly (string Key, DayOfWeek Day)[] Week =
    [
        ("Mon", DayOfWeek.Monday), ("Tue", DayOfWeek.Tuesday), ("Wed", DayOfWeek.Wednesday), ("Thu", DayOfWeek.Thursday),
        ("Fri", DayOfWeek.Friday), ("Sat", DayOfWeek.Saturday), ("Sun", DayOfWeek.Sunday),
    ];

    /// <summary>Senza config l'abitudine è "ogni giorno", come per i numeri della testa.</summary>
    public static string Describe(HabitConfig? config, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (config is null) return Strings.Schedule_Daily;
        return config.ScheduleType switch
        {
            ScheduleType.Daily => Strings.Schedule_Daily,
            ScheduleType.SpecificDays => Days(config.ScheduleDaysJson, culture),
            ScheduleType.WeeklyX => Format(Strings.Schedule_WeeklyX, config.ScheduleWeeklyTarget ?? 0, culture),
            ScheduleType.EveryXDays => Format(Strings.Schedule_EveryXDays, config.ScheduleEveryXDays ?? 0, culture),
            ScheduleType.Monthly => Strings.Schedule_Monthly,
            _ => string.Empty,
        };
    }

    private static string Days(string? json, CultureInfo culture)
    {
        string[] keys;
        try { keys = string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { keys = []; }
        return string.Join(", ", Week
            .Where(w => keys.Contains(w.Key, StringComparer.OrdinalIgnoreCase))
            .Select(w => culture.DateTimeFormat.GetAbbreviatedDayName(w.Day)));
    }

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI strings
    private static string Format(string format, int n, CultureInfo culture) => string.Format(culture, format, n);
#pragma warning restore CA1863
}
