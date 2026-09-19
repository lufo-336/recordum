using System.Globalization;
using Recordum.Core.Enums;

namespace Recordum.Presentation.Services;

/// <summary>Which culture a persisted <see cref="AppCulture"/> choice stands for. The portable half of
/// the culture bootstrap: the WPF shell's <c>CultureBootstrap</c> applies the result to the visual tree
/// (a WPF-only mechanism), while ViewModels only need to resolve a choice, e.g. to preview a format.</summary>
public static class CultureResolution
{
    /// <summary>The culture a choice stands for. <see cref="AppCulture.System"/> defers to the default
    /// the caller supplies — the OS display language for the UI, the OS regional format for numbers —
    /// rather than to one global machine culture, so the two knobs keep Windows' own distinction.</summary>
    public static CultureInfo Resolve(AppCulture choice, CultureInfo systemDefault)
    {
        ArgumentNullException.ThrowIfNull(systemDefault);
        return choice switch
        {
            AppCulture.English => CultureInfo.GetCultureInfo("en-US"),
            AppCulture.Italian => CultureInfo.GetCultureInfo("it-IT"),
            _ => systemDefault,
        };
    }
}
