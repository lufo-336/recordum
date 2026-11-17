using System.Text.RegularExpressions;

namespace Recordum.Core.Text;

public static partial class TagNames
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[^\p{L}\p{N}_-]")]
    private static partial Regex Forbidden();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex Dashes();

    /// <summary>Il tag ricavato dal nome di uno scaglione, o null se non ne esce un nome valido.</summary>
    public static string? FromLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var s = Spaces().Replace(label.ToLowerInvariant(), "-");
        s = Dashes().Replace(Forbidden().Replace(s, ""), "-").Trim('-');
        if (s.Length > InlineTags.MaxLength) s = s[..InlineTags.MaxLength].Trim('-');
        return InlineTags.IsValidName(s) ? s : null;
    }
}
