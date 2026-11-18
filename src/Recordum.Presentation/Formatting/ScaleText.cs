#pragma warning disable CA1720, CA1863 // Short/Long: nomi della spec; il formato viene da una risorsa localizzata
using System.Globalization;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Scales;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;

namespace Recordum.Presentation.Formatting;

/// <summary>L'unico punto che legge una scala: nomi degli scaglioni, numero, lettura breve e lunga, frazione (GIO-23).
/// Cultura corrente.</summary>
public static class ScaleText
{
    /// <summary>Gli scaglioni con nome: ufficiale → da <see cref="OfficialTexts"/>, dell'utente → <c>Label</c>. Senza nome, esclusi.</summary>
    public static IReadOnlyList<NamedAnchor> Named(ScaleTemplate s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var list = new List<NamedAnchor>();
        foreach (var a in s.Anchors)
        {
            var name = s.IsOfficial ? OfficialTexts.ScaleAnchorLabel(s, a.Value) ?? a.Label : a.Label;
            if (!string.IsNullOrWhiteSpace(name)) list.Add(new NamedAnchor(a.Value, name, a.TagId));
        }
        return list;
    }

    public static string Number(ScaleTemplate s, decimal v) =>
        v.ToString(s.Precision == ScalePrecision.Decimal ? "N2" : "N0", CultureInfo.CurrentCulture);

    public static string Short(ScaleTemplate s, decimal v)
    {
        var z = ScaleZone.Read(Named(s), v);
        if (z.Exact is not null) return z.Exact.Name;
        return z.Nearest is null ? Number(s, v) : string.Format(CultureInfo.CurrentCulture, Strings.Scale_Reading_Short, z.Nearest.Name, Number(s, v));
    }

    public static string Long(ScaleTemplate s, decimal v)
    {
        var z = ScaleZone.Read(Named(s), v);
        if (z.Exact is not null) return z.Exact.Name;
        return z.Below is not null && z.Above is not null
            ? string.Format(CultureInfo.CurrentCulture, Strings.Scale_Reading_Between, Number(s, v), z.Below.Name, z.Above.Name)
            : Short(s, v);
    }

    public static double Fraction(ScaleTemplate s, decimal v) =>
        s.MaxValue <= s.MinValue ? 0 : (double)Math.Clamp((v - s.MinValue) / (s.MaxValue - s.MinValue), 0m, 1m);
}
