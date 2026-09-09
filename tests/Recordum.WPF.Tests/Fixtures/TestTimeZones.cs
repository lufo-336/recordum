namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Fusi costruiti a mano: mai FindSystemTimeZoneById, che dipende dal tz database dell'OS
/// (questa suite gira su Windows, su macOS e su Linux). Gemello degli omonimi in Recordum.Core.Tests e
/// Recordum.Data.Tests: i tre progetti di test non si vedono fra loro.</summary>
public static class TestTimeZones
{
    /// <summary>UTC+1 con ora legale +1h dall'ultima domenica di marzo (02:00) all'ultima domenica
    /// di ottobre (03:00) — la forma di Europe/Rome, senza dipendere da Europe/Rome.
    /// Nel 2026: salto in avanti il 29 marzo, indietro il 25 ottobre.</summary>
    public static TimeZoneInfo RomeLike { get; } = BuildRomeLike();

    /// <summary>Fuso a offset fisso, senza ora legale (es. Fixed(2) = UTC+2, Fixed(-5) = UTC-5).</summary>
    public static TimeZoneInfo Fixed(int hours)
    {
        var id = $"Test{(hours >= 0 ? "+" : "")}{hours}";
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(hours), id, id);
    }

    private static TimeZoneInfo BuildRomeLike()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0), month: 3, week: 5, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 3, 0, 0), month: 10, week: 5, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/RomeLike", TimeSpan.FromHours(1), "Rome-like (test)", "CET", "CEST", [rule]);
    }
}
