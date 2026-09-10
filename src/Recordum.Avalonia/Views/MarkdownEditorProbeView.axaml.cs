using Avalonia.Controls;

namespace Recordum.Avalonia.Views;

/// <summary>
/// Sonda editor (branch avalonia-linux-slice): esercita <see cref="Controls.MarkdownEditorControl"/>
/// con un documento realistico — titoli, grassetto/corsivo, una lista, un link, un blocco di codice
/// — così lo screenshot mostra sorgente E anteprima nello stesso frame (deliverable #2 del probe).
/// </summary>
public partial class MarkdownEditorProbeView : UserControl
{
    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs.
    public MarkdownEditorProbeView()
    {
        InitializeComponent();

        Editor.Markdown =
            "# Diario — 10 settembre\n\n" +
            "Oggi ho **finito** la sonda editor. È stata *più semplice* del previsto, a parte una "
            + "sorpresa sul namespace del progetto.\n\n" +
            "Cose fatte:\n\n" +
            "- letto `editor.js` per capire cosa usa davvero Toast UI\n" +
            "- verificato Markdown.Avalonia contro Avalonia 12\n" +
            "- scritto il controllo nativo\n\n" +
            "Dettagli nel [report del probe](https://example.invalid/report).\n\n" +
            "```csharp\n" +
            "var editor = new MarkdownEditorControl { Markdown = \"# ciao\" };\n" +
            "```\n";
    }
}
