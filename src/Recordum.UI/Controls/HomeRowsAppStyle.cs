using Avalonia;
using Avalonia.Styling;
using Recordum.Presentation.ViewModels.Home;

namespace Recordum.UI.Controls;

/// <summary>La versione delle righe scelta in Impostazioni › Aspetto, per le righe d'anteprima fuori dalla Home (creatore,
/// selettore dei modelli, Configura): uno stile dell'applicazione su <see cref="RowPreview"/>. Un valore scritto sulla
/// singola anteprima (la pagina Aspetto, che le mostra tutte e tre) vince sullo stile.</summary>
public static class HomeRowsAppStyle
{
    private static Style? _style;

    public static void Apply(Application app, HomeRows rows)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (_style is not null) app.Styles.Remove(_style);
        _style = new Style(x => x.OfType<RowPreview>()) { Setters = { new Setter(HomeRowsMetrics.RowsProperty, rows) } };
        app.Styles.Add(_style);
    }
}
