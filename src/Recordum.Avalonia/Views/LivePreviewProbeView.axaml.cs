using Avalonia.Controls;

namespace Recordum.Avalonia.Views;

/// <summary>
/// Sonda Live Preview (branch avalonia-linux-slice): esercita
/// <see cref="Controls.LivePreviewEditorControl"/> con un documento realistico — titolo,
/// grassetto+corsivo nella stessa riga, due paragrafi piani — e piazza il caret sulla riga 3
/// (quella con grassetto+corsivo) così lo screenshot mostra nello stesso frame: il titolo reso
/// grande, la riga del caret grezza coi marcatori visibili, i paragrafi piani normali
/// (deliverable #2 del report).
/// </summary>
public partial class LivePreviewProbeView : UserControl
{
    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs.
    public LivePreviewProbeView()
    {
        InitializeComponent();

        Editor.Markdown =
            "# Diario — 10 settembre\n" +
            "\n" +
            "Oggi ho **finito** la sonda live preview. È stata *più semplice* del previsto.\n" +
            "\n" +
            "Riga **non** del caret: qui il grassetto e il *corsivo* devono RENDERSI, marcatori spariti.\n" +
            "\n" +
            "Un'altra riga qualsiasi, ancora piana, per dare respiro al documento.\n";

        // Riga 3 (1-based): "Oggi ho **finito** ... *più semplice* ...". Colonna 10 = dentro
        // "finito", giusto per essere chiaramente sulla riga formattata, non a inizio/fine riga.
        Editor.MoveCaretTo(3, 10);
    }

    /// <summary>Sposta il caret dell'editor interno. Serve al test strutturale per dimostrare che
    /// la riga LASCIATA dal caret si formatta, che è la metà dinamica del comportamento.</summary>
    public void MoveEditorCaretTo(int line, int column) => Editor.MoveCaretTo(line, column);
}
