using System.Text.RegularExpressions;

namespace Recordum.Core.Text;

/// <summary>Estrae i tag <c>#parola</c> dal testo di una voce (spec F2d §5.1). Un <c>#</c> conta solo a inizio
/// parola e seguito da lettere, cifre, <c>_</c> o <c>-</c> (almeno un carattere non cifra, massimo 64); dentro il
/// codice (inline o a blocchi) non conta.</summary>
public static partial class InlineTags
{
    public const int MaxLength = 64;

    [GeneratedRegex(@"```.*?(```|\z)", RegexOptions.Singleline)]
    private static partial Regex FencedCode();

    [GeneratedRegex(@"`[^`\n]*`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"(?<![^\s(\[{""'«“])#([\p{L}\p{N}_-]+)")]
    private static partial Regex Candidate();

    /// <summary>Vero se <paramref name="name"/> scritto come <c>#nome</c> nel testo viene letto da <see cref="Parse"/>
    /// come esattamente quel tag (niente spazi, punteggiatura, solo cifre o oltre <see cref="MaxLength"/>).</summary>
    public static bool IsValidName([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? name)
        => !string.IsNullOrEmpty(name) && Parse("#" + name) is [var only] && string.Equals(only, name, StringComparison.Ordinal);

    /// <summary>I tag in ordine di prima comparsa, distinti senza badare alle maiuscole (vince la prima grafia).</summary>
    public static IReadOnlyList<string> Parse(string markdown)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var (start, length) in Find(markdown))
        {
            var name = markdown.Substring(start + 1, length - 1);
            if (seen.Add(name)) result.Add(name);
        }
        return result;
    }

    /// <summary>Dove stanno i tag nel testo, ripetuti compresi: <c>Start</c> è il <c>#</c>, <c>Length</c> copre
    /// <c>#nome</c>. Stessa regola di <see cref="Parse"/>: l'editor colora esattamente ciò che verrà salvato.</summary>
    public static IReadOnlyList<(int Start, int Length)> Find(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return [];
        // Il codice si maschera con spazi della stessa lunghezza: gli indici restano quelli del testo originale.
        var text = InlineCode().Replace(FencedCode().Replace(markdown, Blank), Blank);
        var result = new List<(int, int)>();
        foreach (Match m in Candidate().Matches(text))
        {
            var name = m.Groups[1].Value;
            if (name.Length > MaxLength || name.All(char.IsDigit)) continue;
            result.Add((m.Index, m.Length));
        }
        return result;
    }

    private static string Blank(Match m) => new(' ', m.Length);
}
