using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Recordum.Avalonia.Controls;

/// <summary>
/// Sonda editor (branch avalonia-linux-slice, NON una feature): sostituto nativo del
/// <c>MarkdownEditorControl</c> WPF (WebView2 + Toast UI Editor vendorizzato, 207 righe) —
/// vedi <c>docs/design/2026-09-09-avalonia-editor-probe.md</c>. Stessa superficie pubblica
/// (<see cref="Markdown"/>, <see cref="IsReadOnly"/>, <see cref="Placeholder"/>) così i tre call
/// site WPF (NotesTabView, HabitScreen quick-add, e lo stand-in in BuilderPreviewPane) si
/// porterebbero senza modifiche al binding. <c>Availability</c> non esiste più: non c'è un
/// runtime WebView2 la cui assenza vada gestita — è una semplificazione, non solo un taglio.
///
/// AvaloniaEdit fa l'editing (sorgente markdown grezzo con syntax highlighting), Markdown.Avalonia
/// fa la resa (sola lettura). Sono due controlli distinti — non un unico documento WYSIWYG come
/// ProseMirror dentro Toast UI. Il ponte fra i due è manuale, qui in code-behind, sullo stesso
/// principio del bridge JS della versione WPF (push esplicito, guardie di rientranza) invece che
/// binding bidirezionale diretto: <see cref="Markdown.Avalonia.MarkdownScrollViewer"/> non è
/// editabile quindi non serve fare data-binding a due vie su di lui, e
/// <c>AvaloniaEdit.TextEditor</c> non ha una StyledProperty "Text" bindabile nativamente (stesso
/// gap che AvalonEdit ha sempre avuto su WPF) — la sincronizzazione fra <see cref="Markdown"/> e
/// il documento dell'editor passa dagli eventi, non dal binding XAML.
/// </summary>
public partial class MarkdownEditorControl : UserControl
{
    private bool _pushingToEditor;
    private bool _pushingToPreview;

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownEditorControl, string>(
            nameof(Markdown), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MarkdownEditorControl, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<MarkdownEditorControl, string>(nameof(Placeholder), string.Empty);

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    static MarkdownEditorControl()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownEditorControl>((c, e) =>
            c.PushToEditorAndPreview((string?)e.NewValue ?? string.Empty));
        IsReadOnlyProperty.Changed.AddClassHandler<MarkdownEditorControl>((c, e) =>
            c.Editor.IsReadOnly = (bool)(e.NewValue ?? false));
        PlaceholderProperty.Changed.AddClassHandler<MarkdownEditorControl>((c, e) =>
            c.PlaceholderText.Text = (string?)e.NewValue ?? string.Empty);
    }

    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): vedi Views/MainWindow.axaml.cs —
    // qui serve perché Editor/Preview/PlaceholderText (x:Name) devono essere cablati.
    public MarkdownEditorControl()
    {
        InitializeComponent();

        Editor.Document.TextChanged += (_, _) =>
        {
            if (_pushingToEditor) return;
            var text = Editor.Document.Text;
            _pushingToPreview = true;
            try { SetCurrentValue(MarkdownProperty, text); }
            finally { _pushingToPreview = false; }
            PushToPreview(text);
            UpdatePlaceholderVisibility(text);
        };

        UpdatePlaceholderVisibility(Markdown);
    }

    private void PushToEditorAndPreview(string md)
    {
        if (_pushingToPreview) return; // il cambio è arrivato dall'editor stesso, già in sync
        if (Editor.Document.Text != md)
        {
            _pushingToEditor = true;
            try { Editor.Document.Text = md; }
            finally { _pushingToEditor = false; }
        }
        PushToPreview(md);
        UpdatePlaceholderVisibility(md);
    }

    private void PushToPreview(string md) => Preview.Markdown = md;

    private void UpdatePlaceholderVisibility(string text) =>
        PlaceholderText.IsVisible = string.IsNullOrEmpty(text);
}
