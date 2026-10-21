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

    /// <summary>I tag in ordine di prima comparsa, distinti senza badare alle maiuscole (vince la prima grafia).</summary>
    public static IReadOnlyList<string> Parse(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return [];
        var text = InlineCode().Replace(FencedCode().Replace(markdown, " "), " ");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (Match m in Candidate().Matches(text))
        {
            var name = m.Groups[1].Value;
            if (name.Length > MaxLength || name.All(char.IsDigit)) continue;
            if (seen.Add(name)) result.Add(name);
        }
        return result;
    }
}
