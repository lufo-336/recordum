using Avalonia.Controls;
using Avalonia.Layout;
using Recordum.Core.HomeRow;
using Recordum.Core.Text;
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

    /// <summary>L'id della foto della galleria che ha la miniatura; <see cref="UnsavedPhotoId"/> non ce l'ha.</summary>
    public static readonly Guid SavedPhotoId = Guid.Parse("5f2b9c1e0d3a4e7f8a6b1c2d3e4f5a6b");
    public static readonly Guid UnsavedPhotoId = Guid.Parse("00000000000000000000000000000abc");

    /// <summary>Il documento del diario, una riga per costrutto (1-based): 1 titolo, 3 grassetto/corsivo/tag,
    /// 4-6 elenco puntato, 7-8 numerato, 9 citazione, 11 foto con miniatura, 12 foto non salvata, 13 riga del cursore.
    /// Il cursore sta sulla riga 13: un solo frame mostra la riga grezza e le altre rese.</summary>
    public static DiaryEditor DiaryEditor(byte[]? thumbnail = null)
    {
        var editor = new DiaryEditor
        {
            Markdown =
                "# Diario — 10 settembre\n" +
                "\n" +
                "Oggi ho **finito** la sonda. È stata *più semplice* del previsto. #lavoro\n" +
                "- uno\n" +
                "* due\n" +
                "+ tre\n" +
                "1. primo\n" +
                "12. dodicesimo\n" +
                "> una citazione che resta in tono attenuato\n" +
                "\n" +
                PhotoRefs.Format(SavedPhotoId, "pranzo.jpg") + "\n" +
                PhotoRefs.Format(UnsavedPhotoId, "persa.jpg") + "\n" +
                "Riga del **cursore**: qui il markdown resta grezzo.",
            Photos = new Dictionary<Guid, byte[]?> { [SavedPhotoId] = thumbnail },
        };
        editor.CaretOffset = editor.Markdown.Length;
        return editor;
    }
}
