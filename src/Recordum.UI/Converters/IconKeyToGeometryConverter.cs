using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Recordum.UI.Converters;

/// <summary>Risolve una chiave d'icona ("Icon.Heart") nella <see cref="Geometry"/> delle risorse
/// dell'applicazione (<c>Themes/Icons.axaml</c>). Chiave sconosciuta, assente o non stringa → null,
/// quindi il <c>Path</c> resta vuoto invece di rompersi. Stessa ricerca dell'
/// <see cref="IntensityStepToBrushConverter"/>: Avalonia vuole la variante di tema anche per una
/// risorsa che non ne dipende.</summary>
public sealed class IconKeyToGeometryConverter : IValueConverter
{
    public static readonly IconKeyToGeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key) return null;

        var app = Application.Current;
        return app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is Geometry g
            ? g
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
