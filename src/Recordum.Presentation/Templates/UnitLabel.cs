using System.Globalization;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.Templates;

public static class UnitLabel
{
    /// <summary>L'unità accanto a un valore: il simbolo se c'è ("2 h"), altrimenti il nome tradotto ("12 pagine");
    /// mai il <c>Code</c>. Il nome ufficiale è scritto da titolo per gli elenchi ("Pagine"): accanto a un valore va
    /// minuscolo. Il nome di un'unità dell'utente resta come l'ha scritto.</summary>
    public static string For(string code, string? symbol, string name, bool isOfficial)
    {
        if (!string.IsNullOrWhiteSpace(symbol)) return symbol;
        var translated = OfficialTexts.UnitName(code, name, isOfficial);
        return isOfficial ? translated.ToLower(CultureInfo.CurrentCulture) : translated;
    }

    public static string For(UnitOfMeasure u)
    {
        ArgumentNullException.ThrowIfNull(u);
        return For(u.Code, u.Symbol, u.Name, u.IsOfficial);
    }

#pragma warning disable CA1863 // CompositeFormat caching not worth it for a once-per-build UI string
    public static string WithValue(string value, string? label) =>
        string.IsNullOrWhiteSpace(label)
            ? value
            : string.Format(CultureInfo.CurrentCulture, Strings.Home_KeyFigure_CounterTotal, value, label).Trim();
#pragma warning restore CA1863
}
