using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace Recordum.WPF.Bootstrap;

/// <summary>Makes one culture govern the whole UI (W2V-1 / W2V-17c).
///
/// <para>WPF does not read <see cref="CultureInfo.CurrentCulture"/>: every binding formats and parses
/// through <see cref="FrameworkElement.LanguageProperty"/>, whose metadata default is a hardcoded
/// en-US. So the app used to show three conventions at once — Italian strings from the .resx
/// (CurrentUICulture), Italian numbers from C# formatting (CurrentCulture, "30,0"), and American dates
/// and number *parsing* from every XAML binding ("8/14/2026"; "2,5" read as 25).</para>
///
/// <para>Applying the culture once, before any window exists, is what the future culture setting
/// (F0-6) will drive: the selector changes where the culture comes from (OS → AppSetting), not how it
/// reaches the visual tree. The metadata default can be overridden only once per process, so changing
/// culture at runtime needs a restart — <see cref="Apply"/> keeps the thread cultures in sync anyway
/// so C#-side formatting follows immediately.</para></summary>
public static class CultureBootstrap
{
    private static bool s_visualTreeLanguageSet;

    /// <summary>The <see cref="XmlLanguage"/> a culture maps to. Separate from <see cref="Apply"/> so
    /// the mapping is testable for cultures the process cannot actually switch to.</summary>
    public static XmlLanguage LanguageFor(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return XmlLanguage.GetLanguage(culture.IetfLanguageTag);
    }

    /// <summary>Makes <paramref name="culture"/> the one the UI formats, parses and sorts by. Call
    /// before creating any window.</summary>
    public static void Apply(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        // OverrideMetadata throws if called twice for the same type, and a process that has already
        // built windows would not pick up a new default anyway: first call wins, by design.
        if (s_visualTreeLanguageSet) return;
        s_visualTreeLanguageSet = true;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(LanguageFor(culture)));
    }
}
