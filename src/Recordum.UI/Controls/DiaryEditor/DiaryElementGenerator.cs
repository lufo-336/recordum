using System;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit.Rendering;

namespace Recordum.UI.Controls;

/// <summary>
/// Sostituisce i marcatori delle righe fuori dal cursore (la scansione è <see cref="DiaryLineScanner"/>): nascosti
/// (<c>#</c> dei titoli, <c>**</c>, <c>*</c>, <c>_</c>), il pallino dell'elenco, la barra della citazione,
/// l'immagine al posto di <c>![nome](photo:id)</c>.
/// <para>Ogni elemento copre nel documento la lunghezza del marcatore: offset, selezione e undo restano quelli del
/// testo. AvaloniaEdit vuole almeno una colonna visuale per elemento, quindi un marcatore nascosto vale una
/// colonna larga zero pixel.</para>
/// </summary>
internal sealed class DiaryElementGenerator(Func<int> caretLine, Func<Guid, Control> photoView)
    : VisualLineElementGenerator
{
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var document = CurrentContext?.Document;
        if (document is null || startOffset >= document.TextLength) return -1;

        var line = document.GetLineByOffset(startOffset);
        if (line.LineNumber == caretLine()) return -1;

        foreach (var marker in DiaryLineScanner.Scan(document.GetText(line)).Markers)
        {
            var absolute = line.Offset + marker.Start;
            if (absolute >= startOffset) return absolute;
        }
        return -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var document = CurrentContext!.Document;
        var line = document.GetLineByOffset(offset);
        foreach (var marker in DiaryLineScanner.Scan(document.GetText(line)).Markers)
        {
            if (line.Offset + marker.Start != offset) continue;
            return marker.Kind switch
            {
                DiaryMarkerKind.Bullet => new FormattedTextElement("• ", marker.Length),
                DiaryMarkerKind.QuoteBar => new InlineObjectElement(marker.Length, QuoteBar()),
                DiaryMarkerKind.Photo => new InlineObjectElement(marker.Length, photoView(marker.PhotoId)),
                _ => new FormattedTextElement(string.Empty, marker.Length),
            };
        }
        return null;
    }

    private Border QuoteBar()
    {
        var bar = new Border
        {
            Width = 3,
            Height = CurrentContext.TextView.DefaultLineHeight,
            Margin = new Thickness(2, 0, 10, 0),
            CornerRadius = new CornerRadius(1.5),
        };
        bar.SetResourceReference(Border.BackgroundProperty, "Brush.TextMuted");
        return bar;
    }
}
