using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;

namespace Recordum.WPF.Bootstrap;

/// <summary>Decides which culture formats the UI and which one it speaks (W2V-1 / W2V-17c, F0-6).
///
/// <para>WPF does not read <see cref="CultureInfo.CurrentCulture"/>: every binding formats and parses
/// through <see cref="FrameworkElement.LanguageProperty"/>, whose metadata default is a hardcoded
/// en-US. So the app used to show three conventions at once — Italian strings from the .resx
/// (CurrentUICulture), Italian numbers from C# formatting (CurrentCulture, "30,0"), and American dates
/// and number *parsing* from every XAML binding ("8/14/2026"; "2,5" read as 25).</para>
///
/// <para>The two choices stay separate all the way down, the way Windows keeps display language and
/// regional format apart: the language picks the .resx, the format picks dates, numbers and therefore
/// which decimal separator <c>NumericInputRule</c> accepts. The metadata default can be overridden
/// only once per process, which is why both choices take effect at the next start.</para></summary>
public static class CultureBootstrap
{
    private static bool s_visualTreeLanguageSet;

    /// <summary>The culture a choice stands for; the resolution itself lives in Recordum.Presentation
    /// (<see cref="CultureResolution"/>) so ViewModels can use it without the WPF shell.</summary>
    public static CultureInfo Resolve(AppCulture choice, CultureInfo systemDefault) =>
        CultureResolution.Resolve(choice, systemDefault);

    /// <summary>The <see cref="XmlLanguage"/> a culture maps to. Separate from <see cref="Apply"/> so
    /// the mapping is testable for cultures the process cannot actually switch to.</summary>
    public static XmlLanguage LanguageFor(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return XmlLanguage.GetLanguage(culture.IetfLanguageTag);
    }

    /// <summary>Makes <paramref name="format"/> the culture the UI formats, parses and sorts by, and
    /// <paramref name="language"/> the one it reads its strings in. Call before creating any window.</summary>
    public static void Apply(CultureInfo format, CultureInfo language)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(language);

        CultureInfo.CurrentCulture = format;
        CultureInfo.DefaultThreadCurrentCulture = format;
        CultureInfo.CurrentUICulture = language;
        CultureInfo.DefaultThreadCurrentUICulture = language;

        // OverrideMetadata throws if called twice for the same type, and a process that has already
        // built windows would not pick up a new default anyway: first call wins, by design.
        if (s_visualTreeLanguageSet) return;
        s_visualTreeLanguageSet = true;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(LanguageFor(format)));
    }

    /// <summary>Both cultures at once, from the persisted choices. The system defaults come from the
    /// process as Windows started it: <see cref="CultureInfo.CurrentCulture"/> is the regional format,
    /// <see cref="CultureInfo.CurrentUICulture"/> the display language.</summary>
    public static void Apply(AppCulture format, AppCulture language) =>
        Apply(Resolve(format, CultureInfo.CurrentCulture), Resolve(language, CultureInfo.CurrentUICulture));
}
