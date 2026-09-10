using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Recordum.UI.Controls;

/// <summary>
/// Sonda Live Preview (branch avalonia-linux-slice, NON una feature): estende l'esperimento
/// <c>MarkdownEditorControl</c> (sorgente+anteprima separati) con un tentativo di "Live
/// Preview" alla Obsidian — un solo <c>AvaloniaEdit.TextEditor</c> il cui testo si formatta
/// mentre si digita (titoli grandi, grassetto vero, marcatori nascosti) TRANNE sulla riga dove
/// sta il caret, che torna grezza per poterla editare. Vedi
/// <c>docs/design/2026-09-10-avalonia-live-preview-probe.md</c> per il "come" e il "quanto costa".
/// </summary>
public partial class LivePreviewEditorControl : UserControl
{
    private bool _pushingText;
    private int _caretLine = 1;

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<LivePreviewEditorControl, string>(
            nameof(Markdown), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    static LivePreviewEditorControl()
    {
        MarkdownProperty.Changed.AddClassHandler<LivePreviewEditorControl>((c, e) =>
            c.PushToEditor((string?)e.NewValue ?? string.Empty));
    }

    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs —
    // qui serve perché Editor (x:Name) deve essere cablato.
    public LivePreviewEditorControl()
    {
        InitializeComponent();

        // Stile (peso/dimensione) e nascondimento dei marcatori sono due hook distinti di
        // AvaloniaEdit: il primo dipinge, il secondo sostituisce intervalli di testo.
        Editor.TextArea.TextView.LineTransformers.Add(
            new MarkdownLivePreviewColorizer(() => _caretLine));
        Editor.TextArea.TextView.ElementGenerators.Add(
            new MarkdownLivePreviewMarkerGenerator(() => _caretLine));

        // Il caret si muove per digitazione/click reali (evento) MA anche programmaticamente
        // dal probe (MoveCaretTo) per dimostrare l'eccezione nello screenshot — PositionChanged
        // copre entrambi i casi perché scatta comunque quando la Line cambia.
        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var newLine = Editor.TextArea.Caret.Line;
            if (newLine == _caretLine) return;
            var oldLine = _caretLine;
            _caretLine = newLine;
            RedrawLine(oldLine);
            RedrawLine(newLine);
        };

        Editor.Document.TextChanged += (_, _) =>
        {
            if (_pushingText) return;
            SetCurrentValue(MarkdownProperty, Editor.Document.Text);
        };
    }

    /// <summary>Riposiziona il caret via API (non un click/tasto reale) — usato dal probe per
    /// mettere in scena l'eccezione riga-del-caret nello screenshot headless (deliverable #2 del
    /// report), dove non c'è un mouse/tastiera reale a generare l'evento da solo.</summary>
    public void MoveCaretTo(int line, int column)
    {
        Editor.TextArea.Caret.Line = line;
        Editor.TextArea.Caret.Column = column;
    }

    private void RedrawLine(int lineNumber)
    {
        var document = Editor.Document;
        if (lineNumber < 1 || lineNumber > document.LineCount) return;
        Editor.TextArea.TextView.Redraw(document.GetLineByNumber(lineNumber));
    }

    private void PushToEditor(string markdown)
    {
        if (Editor.Document.Text == markdown) return;
        _pushingText = true;
        try { Editor.Document.Text = markdown; }
        finally { _pushingText = false; }
    }
}
