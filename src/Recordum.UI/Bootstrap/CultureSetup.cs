using System.Globalization;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;

namespace Recordum.UI.Bootstrap;

/// <summary>La meta' portabile di <c>CultureBootstrap</c>, e l'unica che serve qui.
///
/// <para>WPF non legge <see cref="CultureInfo.CurrentCulture"/>: ogni binding formatta e interpreta
/// attraverso <c>FrameworkElement.Language</c>, il cui default di metadato e' en-US — da li' venivano
/// le date americane in un'app italiana (W2V-1). <b>Avalonia non ha quel difetto</b>: i binding
/// seguono la cultura del thread dell'interfaccia (misurato). Quindi resta solo da impostare la
/// cultura, prima della prima finestra e dopo l'inizializzazione del database, perche' le scelte
/// vivono negli AppSetting.</para></summary>
public static class CultureSetup
{
    /// <summary>La cultura che formatta e quella in cui l'app parla, tenute separate come le tiene
    /// Windows. Da chiamare prima di creare qualunque finestra.</summary>
    public static void Apply(CultureInfo format, CultureInfo language)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(language);
        CultureInfo.CurrentCulture = format;
        CultureInfo.DefaultThreadCurrentCulture = format;
        CultureInfo.CurrentUICulture = language;
        CultureInfo.DefaultThreadCurrentUICulture = language;
    }

    /// <summary>Le due scelte persistite. I default di sistema arrivano dal processo come il sistema
    /// operativo l'ha avviato: <see cref="CultureInfo.CurrentCulture"/> e' il formato regionale,
    /// <see cref="CultureInfo.CurrentUICulture"/> la lingua di visualizzazione.</summary>
    public static void Apply(AppCulture format, AppCulture language)
        => Apply(CultureResolution.Resolve(format, CultureInfo.CurrentCulture),
                 CultureResolution.Resolve(language, CultureInfo.CurrentUICulture));
}
