using System.Globalization;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;

namespace Recordum.Presentation.Services;

public static class CompletionDay
{
    /// <summary>Le registrazioni di <paramref name="day"/> nel confine dell'abitudine. RangeOf è semiaperta, il filtro
    /// del servizio inclusivo al millisecondo: -1 ms.</summary>
    public static ValueTask<IReadOnlyList<Completion>> ListAsync(
        ICompletionService completions, Guid habitNodeId, DayBoundary boundary, DateOnly day, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(boundary);
        var (from, toExclusive) = boundary.RangeOf(day);
        return completions.ListByHabitAsync(habitNodeId, from, toExclusive.AddMilliseconds(-1), ct: ct);
    }

    /// <summary>Il giorno per esteso, senza l'anno: "lunedì 9 novembre" / "Monday November 9".</summary>
    public static string LongLabel(DateOnly day, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var date = day.ToDateTime(TimeOnly.MinValue);
        return $"{date.ToString("dddd", culture)} {date.ToString(culture.DateTimeFormat.MonthDayPattern, culture)}";
    }
}
