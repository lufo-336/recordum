using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Recordum.UI.Controls;

/// <summary>
/// Sonda Live Preview (branch avalonia-linux-slice, NON una feature): applica peso/dimensione
/// del font a titoli/grassetto/corsivo — SENZA nascondere i marcatori, quello è compito
/// gemello di <see cref="MarkdownLivePreviewMarkerGenerator"/> (stessa fonte di scansione,
/// <see cref="MarkdownLineScanner"/>, così le due classi non possono desincronizzarsi).
///
/// Hook AvaloniaEdit: <c>DocumentColorizingTransformer</c>, aggiunto a
/// <c>TextView.LineTransformers</c> — chiamato per ogni riga visuale in fase di costruzione,
/// PRIMA che <see cref="MarkdownLivePreviewMarkerGenerator"/> decida cosa nascondere.
///
/// Sorpresa verificata per riflessione sul pacchetto (vedi report, "Anything that broke or
/// surprised you"): <c>VisualLineElementTextRunProperties</c> NON ha <c>SetFontWeight</c>/
/// <c>SetFontStyle</c> — solo <c>SetTypeface(Typeface)</c>, che porta famiglia+stile+peso
/// insieme. Bold/corsivo/titoli passano quindi da un <c>Typeface</c> nuovo costruito a partire
/// da quello esistente, non da mutator dedicati (stesso limite dell'AvalonEdit WPF originale).
/// </summary>
internal sealed class MarkdownLivePreviewColorizer : DocumentColorizingTransformer
{
    private readonly Func<int> _caretLine;

    public MarkdownLivePreviewColorizer(Func<int> caretLine) => _caretLine = caretLine;

    internal static int InvocationCount;

    protected override void ColorizeLine(DocumentLine line)
    {
        InvocationCount++;
        // Eccezione riga-del-caret: niente stile, il testo resta a dimensione/peso normali così
        // il markdown grezzo (che MarkdownLivePreviewMarkerGenerator lascia visibile sulla
        // stessa riga) si legge come testo piano, non come titolo/grassetto a metà.
        if (line.LineNumber == _caretLine()) return;

        var text = CurrentContext.Document.GetText(line);
        var lineStart = line.Offset;

        var heading = MarkdownLineScanner.MatchHeading(text);
        if (heading != null)
        {
            var size = heading.Value.Level switch { 1 => 22.0, 2 => 19.0, _ => 17.0 };
            var contentStart = lineStart + heading.Value.MarkerLength;
            if (contentStart < line.EndOffset)
            {
                ChangeLinePart(contentStart, line.EndOffset, element =>
                {
                    var tf = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(
                        new Typeface(tf.FontFamily, tf.Style, FontWeight.SemiBold, tf.Stretch));
                    element.TextRunProperties.SetFontRenderingEmSize(size);
                });
            }
            return; // un titolo non porta anche bold/italic dentro: fuori budget del probe
        }

        foreach (var span in MarkdownLineScanner.MatchInlineSpans(text))
        {
            var start = lineStart + span.Start;
            var end = lineStart + span.End;
            if (start >= end) continue;
            ChangeLinePart(start, end, element =>
            {
                var tf = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(span.IsBold
                    ? new Typeface(tf.FontFamily, tf.Style, FontWeight.Bold, tf.Stretch)
                    : new Typeface(tf.FontFamily, FontStyle.Italic, tf.Weight, tf.Stretch));
            });
        }
    }
}
