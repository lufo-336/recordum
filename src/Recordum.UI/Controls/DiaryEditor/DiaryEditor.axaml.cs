using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Controls;

/// <summary>
/// L'editor del diario (decisione 9): un solo <c>AvaloniaEdit.TextEditor</c> il cui markdown si rende mentre si
/// scrive (titoli, grassetto, corsivo, elenchi, citazioni, <c>#tag</c>, immagini) tranne sulla riga del cursore,
/// che resta grezza. Ctrl+B / Ctrl+I avvolgono la selezione; Invio su una voce d'elenco ne apre un'altra.
/// <para><b>Tastiera dell'host</b> (menu <c>#</c>): chi ospita il controllo si aggancia con
/// <c>editor.AddHandler(KeyDownEvent, handler, RoutingStrategies.Tunnel)</c>. Il tunnel arriva al
/// <see cref="DiaryEditor"/> prima che all'area di testo, dove l'editor gestisce i suoi tasti: un tasto che l'host
/// marca <c>Handled</c> non arriva né alle scorciatoie né all'a capo.</para>
/// </summary>
public partial class DiaryEditor : UserControl
{
    private bool _pushingText;
    private int _caretLine = 1;
    // Una decodifica per id; se cambia il contenuto (un altro array) si decodifica di nuovo. Le bitmap sono del
    // controllo: si liberano quando l'id esce da Photos o il contenuto cambia.
    private readonly Dictionary<Guid, (byte[] Source, Bitmap? Bitmap)> _bitmaps = [];
    // La larghezza offerta al controllo nell'ultima misura. AvaloniaEdit misura un oggetto in linea senza limiti e
    // costruisce le righe durante la misura, prima di qualunque Bounds: il tetto dell'immagine si prende da qui.
    private double _availableWidth = double.PositiveInfinity;

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<DiaryEditor, string>(nameof(Markdown), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<DiaryEditor, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, byte[]?>?> PhotosProperty =
        AvaloniaProperty.Register<DiaryEditor, IReadOnlyDictionary<Guid, byte[]?>?>(nameof(Photos));

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Sola lettura: nessuna riga del cursore, tutto il testo è reso.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Le miniature per id. Un id assente è "foto non salvata"; un id con contenuto null (o illeggibile) è
    /// "foto non disponibile".</summary>
    public IReadOnlyDictionary<Guid, byte[]?>? Photos
    {
        get => GetValue(PhotosProperty);
        set => SetValue(PhotosProperty, value);
    }

    public int CaretOffset
    {
        get => Editor.CaretOffset;
        set => Editor.CaretOffset = Math.Clamp(value, 0, Editor.Document.TextLength);
    }

    /// <summary>Il testo o il cursore sono cambiati (per digitazione, per <see cref="ReplaceText"/> o per binding).</summary>
    public event EventHandler? TextOrCaretChanged;

    static DiaryEditor()
    {
        MarkdownProperty.Changed.AddClassHandler<DiaryEditor>((c, e) => c.PushToEditor((string?)e.NewValue ?? string.Empty));
        IsReadOnlyProperty.Changed.AddClassHandler<DiaryEditor>((c, e) =>
        {
            c.Editor.IsReadOnly = e.NewValue is true;
            c.Editor.TextArea.TextView.Redraw();
        });
        PhotosProperty.Changed.AddClassHandler<DiaryEditor>((c, _) =>
        {
            c.PruneBitmaps();
            c.Editor.TextArea.TextView.Redraw();
        });
    }

    public DiaryEditor()
    {
        InitializeComponent();

        var view = Editor.TextArea.TextView;
        view.LineTransformers.Add(new DiaryColorizer(RawLine, Brush));
        view.ElementGenerators.Add(new DiaryElementGenerator(RawLine, PhotoView));
        ActualThemeVariantChanged += (_, _) => view.Redraw();

        Editor.TextArea.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);

        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var newLine = Editor.TextArea.Caret.Line;
            if (newLine != _caretLine)
            {
                var oldLine = _caretLine;
                _caretLine = newLine;
                RedrawLine(oldLine);
                RedrawLine(newLine);
            }
            TextOrCaretChanged?.Invoke(this, EventArgs.Empty);
            BringCaretIntoView();
        };

        Editor.Document.TextChanged += (_, _) =>
        {
            if (!_pushingText) SetCurrentValue(MarkdownProperty, Editor.Document.Text);
            TextOrCaretChanged?.Invoke(this, EventArgs.Empty);
            BringCaretIntoView();
        };
    }

    private bool _bringPending;

    // Dentro una lista l'editor cresce col testo e il suo ScrollViewer non scorre mai: il cursore lo deve mostrare chi
    // contiene l'editor. BringIntoView sale dal controllo, quindi raggiunge lo ScrollViewer esterno. Dopo il layout, cosi'
    // la riga nuova c'e' gia'; solo con il fuoco dentro, cosi' un testo dal binding non fa scorrere la colonna.
    private void BringCaretIntoView()
    {
        if (_bringPending || !IsKeyboardFocusWithin) return;
        _bringPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _bringPending = false;
            if (CaretBounds() is { } caret) this.BringIntoView(caret.Inflate(new Thickness(0, 4)));
        }, DispatcherPriority.Background);
    }

    /// <summary>Il rettangolo del cursore nelle coordinate del controllo (per ancorare il menu <c>#</c>); null finché
    /// l'editor non ha un layout.</summary>
    public Rect? CaretBounds()
    {
        var view = Editor.TextArea.TextView;
        if (view.Bounds.Width <= 0) return null;
        var position = Editor.TextArea.Caret.Position;
        var top = view.GetVisualPosition(position, VisualYPosition.LineTop) - view.ScrollOffset;
        var bottom = view.GetVisualPosition(position, VisualYPosition.LineBottom) - view.ScrollOffset;
        return view.TranslatePoint(top, this) is { } p ? new Rect(p.X, p.Y, 1, bottom.Y - top.Y) : null;
    }

    /// <summary>Sostituisce tutto il testo (un passo solo di annulla) e mette il cursore a <paramref name="caret"/>.</summary>
    public void ReplaceText(string text, int caret)
    {
        Editor.Document.Replace(0, Editor.Document.TextLength, text);
        CaretOffset = caret;
    }

    private int RawLine() => IsReadOnly ? -1 : _caretLine;

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        _availableWidth = availableSize.Width;
        return base.MeasureOverride(availableSize);
    }

    private Control PhotoView(Guid id)
    {
        if (Photos is null || !Photos.TryGetValue(id, out var bytes)) return Placeholder(Strings.Diario_Editor_PhotoNotSaved);
        if (Decode(id, bytes) is not { } bitmap) return Placeholder(Strings.Diario_Photo_Missing);
        return new Image
        {
            Source = bitmap,
            // ponytail: margine fisso per Padding dell'editor (2 x 10) e barra di scorrimento.
            MaxWidth = Math.Max(1, _availableWidth - 40),
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
    }

    private Bitmap? Decode(Guid id, byte[]? bytes)
    {
        if (bytes is null) return null;
        if (_bitmaps.TryGetValue(id, out var cached))
        {
            if (ReferenceEquals(cached.Source, bytes)) return cached.Bitmap;
            cached.Bitmap?.Dispose();
        }
        Bitmap? bitmap;
        try { bitmap = new Bitmap(new MemoryStream(bytes)); }
        // Una miniatura che Skia non sa leggere si mostra come mancante: il testo resta modificabile.
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        { bitmap = null; }
        _bitmaps[id] = (bytes, bitmap);
        return bitmap;
    }

    // Le righe si ricostruiscono (Redraw) prima del prossimo disegno, quindi nessuna Image mostra una bitmap liberata.
    private void PruneBitmaps()
    {
        foreach (var id in _bitmaps.Keys.Where(id => Photos is null || !Photos.ContainsKey(id)).ToList())
        {
            _bitmaps[id].Bitmap?.Dispose();
            _bitmaps.Remove(id);
        }
    }

        private static TextBlock Placeholder(string text)
    {
        var block = new TextBlock { Text = text, FontStyle = FontStyle.Italic };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextMuted");
        return block;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsReadOnly) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.B or Key.I)
        {
            Wrap(e.Key == Key.B ? "**" : "*");
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = ContinueList();
        }
    }

    /// <summary>Avvolge la selezione nei marcatori; senza selezione inserisce la coppia con il cursore in mezzo.</summary>
    private void Wrap(string marker)
    {
        var start = Editor.SelectionStart;
        var length = Editor.SelectionLength;
        Editor.Document.Replace(start, length, marker + Editor.Document.GetText(start, length) + marker);
        if (length == 0) Editor.CaretOffset = start + marker.Length;
        else Editor.Select(start + marker.Length, length);
    }

    /// <summary>Invio su una voce d'elenco: apre la voce successiva (numero +1); su una voce vuota svuota la riga e
    /// chiude l'elenco. Falso = non è un elenco, l'a capo lo fa l'editor.</summary>
    private bool ContinueList()
    {
        if (Editor.SelectionLength > 0) return false;
        var document = Editor.Document;
        var line = document.GetLineByOffset(Editor.CaretOffset);
        var text = document.GetText(line);
        var scan = DiaryLineScanner.Scan(text);
        if (scan.Block is not (DiaryBlock.Bullet or DiaryBlock.Numbered)) return false;
        if (Editor.CaretOffset - line.Offset < scan.MarkerLength) return false;

        if (string.IsNullOrWhiteSpace(text[scan.MarkerLength..]))
        {
            document.Replace(line.Offset, line.Length, string.Empty);
            return true;
        }

        var next = TextUtilities.GetNewLineFromDocument(document, line.LineNumber)
            + (scan.Block == DiaryBlock.Bullet ? text[..scan.MarkerLength] : $"{scan.Level + 1}. ");
        var at = Editor.CaretOffset;
        document.Insert(at, next);
        Editor.CaretOffset = at + next.Length;
        return true;
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
        try
        {
            Editor.Document.Text = markdown;
            // Un testo portato dal binding (voce da modificare, bozza, testo dopo Salva) non si annulla: Ctrl+Z
            // riporterebbe il testo di prima sotto un'altra voce. ReplaceText invece resta annullabile.
            Editor.Document.UndoStack.ClearAll();
        }
        finally { _pushingText = false; }
    }
}
