using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Recordum.Core.Text;

namespace Recordum.UI.Controls;

/// <summary>Il tipo di blocco di una riga: decide il marcatore iniziale e il suo aspetto.</summary>
internal enum DiaryBlock { Paragraph, Heading, Bullet, Numbered, Quote }

/// <summary>Che cosa prende il posto di un marcatore fuori dalla riga del cursore.</summary>
internal enum DiaryMarkerKind { Hidden, Bullet, QuoteBar, Photo }

/// <summary>Un tratto della riga che il generatore sostituisce. Offset relativi all'inizio della riga.</summary>
internal readonly record struct DiaryMarker(int Start, int Length, DiaryMarkerKind Kind, Guid PhotoId = default);

/// <summary>Il CONTENUTO di un grassetto o corsivo (senza marcatori), offset relativi all'inizio della riga.</summary>
internal readonly record struct InlineSpan(int Start, int End, bool IsBold);

/// <param name="Level">Livello del titolo (1-3) o numero della voce numerata; 0 altrimenti.</param>
/// <param name="MarkerLength">Lunghezza del marcatore di blocco (<c>"# "</c>, <c>"- "</c>, <c>"12. "</c>, <c>"> "</c>).</param>
internal sealed record DiaryLine(
    DiaryBlock Block,
    int Level,
    int MarkerLength,
    IReadOnlyList<InlineSpan> Spans,
    IReadOnlyList<(int Start, int Length)> Tags,
    IReadOnlyList<DiaryMarker> Markers);

/// <summary>
/// Legge UNA riga del diario. È la fonte unica di "che cosa è marcatura" per i due consumer che devono restare
/// d'accordo: <see cref="DiaryColorizer"/> (stile) e <see cref="DiaryElementGenerator"/> (che cosa sostituire), e
/// per la tastiera di <see cref="DiaryEditor"/> (Invio su una voce d'elenco).
/// <para>Perimetro della decisione 9: titoli 1-3, elenchi, citazioni, grassetto/corsivo non annidati, <c>#tag</c>
/// (stessa regola di <see cref="InlineTags"/>), immagini (<see cref="PhotoRefs"/>).</para>
/// </summary>
// ponytail: una riga alla volta, quindi un blocco ``` su più righe non maschera i #tag che contiene (il salvataggio
// sì). Il blocco di codice è fuori dal perimetro dell'editor; se entra, il colorizer deve vedere il documento.
internal static partial class DiaryLineScanner
{
    [GeneratedRegex(@"^(#{1,3}) +")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^[-*+] ")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(\d{1,9})\. ")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"^> ?")]
    private static partial Regex Quote();

    [GeneratedRegex(@"\*\*([^*\n]+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)([^*\n]+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicStar();

    [GeneratedRegex(@"(?<![\w_])_([^_\n]+?)_(?![\w_])")]
    private static partial Regex ItalicUnderscore();

    public static DiaryLine Scan(string line)
    {
        var markers = new List<DiaryMarker>();
        var (block, level, markerLength) = MatchBlock(line);
        switch (block)
        {
            case DiaryBlock.Heading: markers.Add(new(0, markerLength, DiaryMarkerKind.Hidden)); break;
            case DiaryBlock.Bullet: markers.Add(new(0, markerLength, DiaryMarkerKind.Bullet)); break;
            case DiaryBlock.Quote: markers.Add(new(0, markerLength, DiaryMarkerKind.QuoteBar)); break;
        }

        // Le immagini prima di tutto: dentro "![nome](photo:…)" niente è grassetto, corsivo o tag.
        var taken = new List<(int Start, int End)>();
        foreach (var photo in PhotoRefs.Find(line))
        {
            if (photo.Start < markerLength) continue;
            markers.Add(new(photo.Start, photo.Length, DiaryMarkerKind.Photo, photo.Id));
            taken.Add((photo.Start, photo.Start + photo.Length));
        }

        // Il grassetto per primo e le sue porzioni escluse dal corsivo, altrimenti **x** sarebbe anche *x*.
        // Si cerca dopo il marcatore di blocco: "* uno" è un elenco, non l'inizio di un corsivo.
        var spans = new List<InlineSpan>();
        Collect(Bold(), 2);
        Collect(ItalicStar(), 1);
        Collect(ItalicUnderscore(), 1);
        spans.Sort((a, b) => a.Start.CompareTo(b.Start));

        var tags = new List<(int Start, int Length)>();
        foreach (var tag in InlineTags.Find(line))
            if (tag.Start >= markerLength && !Overlaps(tag.Start, tag.Start + tag.Length)) tags.Add(tag);

        markers.Sort((a, b) => a.Start.CompareTo(b.Start));
        return new DiaryLine(block, level, markerLength, spans, tags, markers);

        void Collect(Regex regex, int width)
        {
            foreach (Match m in regex.Matches(line, markerLength))
            {
                if (Overlaps(m.Index, m.Index + m.Length)) continue;
                var content = m.Groups[1];
                spans.Add(new InlineSpan(content.Index, content.Index + content.Length, width == 2));
                markers.Add(new(m.Index, width, DiaryMarkerKind.Hidden));
                markers.Add(new(content.Index + content.Length, width, DiaryMarkerKind.Hidden));
                taken.Add((m.Index, m.Index + m.Length));
            }
        }

        bool Overlaps(int start, int end) => taken.Exists(t => start < t.End && end > t.Start);
    }

    private static (DiaryBlock Block, int Level, int MarkerLength) MatchBlock(string line)
    {
        if (Heading().Match(line) is { Success: true } h) return (DiaryBlock.Heading, h.Groups[1].Length, h.Length);
        if (Bullet().Match(line) is { Success: true } b) return (DiaryBlock.Bullet, 0, b.Length);
        if (Numbered().Match(line) is { Success: true } n) return (DiaryBlock.Numbered, int.Parse(n.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), n.Length);
        if (Quote().Match(line) is { Success: true } q) return (DiaryBlock.Quote, 0, q.Length);
        return (DiaryBlock.Paragraph, 0, 0);
    }
}
