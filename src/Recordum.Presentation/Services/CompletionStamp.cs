using Recordum.Core.Abstractions;
using Recordum.Core.Time;

namespace Recordum.Presentation.Services;

/// <summary>Il timbro di una registrazione, in un punto solo. Una registrazione per oggi si stampiglia
/// <b>adesso</b>; una retroattiva per il giorno D a <b>mezzogiorno locale di D</b> (spec R9 §5).</summary>
public static class CompletionStamp
{
    public static (DateTime Timestamp, long Ms, bool IsRetroactive) For(
        DayBoundary boundary, IClock clock, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(clock);

        if (day == boundary.Today(clock))
        {
            var now = clock.UtcNow;
            return (now.UtcDateTime, now.ToUnixTimeMilliseconds(), false);
        }

        var noon = boundary.NoonOf(day);
        return (noon.UtcDateTime, noon.ToUnixTimeMilliseconds(), true);
    }
}
