using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Recordum.UI.Controls;

/// <summary>
/// Lo stile delle righe fuori dal cursore: titoli più grandi, grassetto e corsivo veri, numero dell'elenco e
/// citazione in tono attenuato, <c>#tag</c> nel colore d'accento. Non nasconde niente: quello è
/// <see cref="DiaryElementGenerator"/>, sulla stessa scansione (<see cref="DiaryLineScanner"/>).
/// <para><c>VisualLineElementTextRunProperties</c> non ha <c>SetFontWeight</c>/<c>SetFontStyle</c>, solo
/// <c>SetTypeface</c>: peso e stile passano da un <c>Typeface</c> nuovo costruito su quello esistente.</para>
/// </summary>
internal sealed class DiaryColorizer(Func<int> caretLine, Func<string, IBrush?> brush) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        // Riga del cursore: grezza, così il markdown si legge come testo piano mentre lo si scrive.
        if (line.LineNumber == caretLine()) return;

        var start = line.Offset;
        var end = line.EndOffset;
        var scan = DiaryLineScanner.Scan(CurrentContext.Document.GetText(line));

        switch (scan.Block)
        {
            case DiaryBlock.Heading:
                var size = scan.Level switch { 1 => 22.0, 2 => 19.0, _ => 17.0 };
                Change(start + scan.MarkerLength, end, e =>
                {
                    var tf = e.TextRunProperties.Typeface;
                    e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, tf.Style, FontWeight.SemiBold, tf.Stretch));
                    e.TextRunProperties.SetFontRenderingEmSize(size);
                });
                break;
            case DiaryBlock.Numbered:
                Foreground(start, start + scan.MarkerLength, "Brush.TextMuted");
                break;
            case DiaryBlock.Quote:
                Foreground(start + scan.MarkerLength, end, "Brush.TextMuted");
                break;
        }

        foreach (var span in scan.Spans)
        {
            Change(start + span.Start, start + span.End, e =>
            {
                var tf = e.TextRunProperties.Typeface;
                e.TextRunProperties.SetTypeface(span.IsBold
                    ? new Typeface(tf.FontFamily, tf.Style, FontWeight.Bold, tf.Stretch)
                    : new Typeface(tf.FontFamily, FontStyle.Italic, tf.Weight, tf.Stretch));
            });
        }

        foreach (var (tagStart, tagLength) in scan.Tags)
            Foreground(start + tagStart, start + tagStart + tagLength, "Brush.Accent");
    }

    private void Foreground(int from, int to, string key)
    {
        if (brush(key) is { } b) Change(from, to, e => e.TextRunProperties.SetForegroundBrush(b));
    }

    private void Change(int from, int to, Action<VisualLineElement> action)
    {
        if (from < to) ChangeLinePart(from, to, action);
    }
}
