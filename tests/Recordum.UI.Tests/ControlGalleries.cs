using Avalonia.Controls;
using Avalonia.Layout;
using Recordum.Core.HomeRow;
using Recordum.UI.Controls;

namespace Recordum.UI.Tests;

/// <summary>Le composizioni che le quattro view di sonda montavano a schermo, ridotte a quello che
/// esercitavano davvero: il controllo, con dati realistici.
///
/// <para>Le view di sonda (`HeatmapStripProbeView`, `MarkdownEditorProbeView`,
/// `LivePreviewProbeView`, `ThemeProbeView`) erano impalcatura: vivevano in `src/Recordum.UI/Views`
/// senza che nessuna parte dell'applicazione le referenziasse. Con la shell vera in piedi sono
/// morte, ma la copertura che portavano no: sta qui, in tre metodi di un file di test.</para></summary>
internal static class ControlGalleries
{
    /// <summary>Le cinque forme per-tipo della striscia, con valori NON uniformi: le mini-barre del
    /// counter e la ramp dello state esistono per mostrare la magnitudine, quindi una serie piatta
    /// non proverebbe niente. Il numero di giorni segue `HomeRowPresentation.ResolveStrip`: 14 per
    /// la resistenza, 7 per gli altri quattro tipi.</summary>
    public static Control HeatmapStripGallery()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new(16) };
        AddRow(panel, "Action", StripStyle.Squares, [1, 0, 1, 1, 1, 0, 1]);
        AddRow(panel, "Counter", StripStyle.Bars, [0.2, 0.5, 0d, 0.8, 1.0, 0.35, 0.65]);
        AddRow(panel, "Resistance", StripStyle.Band, [1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1]);
        AddRow(panel, "Journal", StripStyle.Dots, [1, 0, 1, 1, 0, 0, 1]);
        AddRow(panel, "State", StripStyle.Ramp, [0.1, 0.9, 0.5, 0d, 0.7, 0.3, 1.0]);
        return panel;

        static void AddRow(Panel host, string label, StripStyle kind, double[] fractions)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            row.Children.Add(new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new HeatmapStrip { Kind = kind, Fractions = [.. fractions] });
            host.Children.Add(row);
        }
    }

    /// <summary>Il documento dell'editor: titoli, grassetto/corsivo, lista, link, blocco di codice.</summary>
    public static MarkdownEditorControl MarkdownEditor() => new()
    {
        Markdown =
            "# Diario — 10 settembre\n\n" +
            "Oggi ho **finito** la sonda editor. È stata *più semplice* del previsto.\n\n" +
            "Cose fatte:\n\n" +
            "- letto `editor.js` per capire cosa usa davvero Toast UI\n" +
            "- verificato Markdown.Avalonia contro Avalonia 12\n" +
            "- scritto il controllo nativo\n\n" +
            "Dettagli nel [report del probe](https://example.invalid/report).\n\n" +
            "```csharp\n" +
            "var editor = new MarkdownEditorControl { Markdown = \"# ciao\" };\n" +
            "```\n",
    };

    /// <summary>Il documento della Live Preview, col caret piazzato sulla riga 3 (quella con
    /// grassetto e corsivo): un solo frame mostra entrambi i comportamenti — la riga del caret
    /// grezza, le altre rese.</summary>
    public static LivePreviewEditorControl LivePreviewEditor()
    {
        var editor = new LivePreviewEditorControl
        {
            Markdown =
                "# Diario — 10 settembre\n" +
                "\n" +
                "Oggi ho **finito** la sonda live preview. È stata *più semplice* del previsto.\n" +
                "\n" +
                "Riga **non** del caret: qui il grassetto e il *corsivo* devono RENDERSI, marcatori spariti.\n" +
                "\n" +
                "Un'altra riga qualsiasi, ancora piana, per dare respiro al documento.\n",
        };
        editor.MoveCaretTo(3, 10);
        return editor;
    }
}
