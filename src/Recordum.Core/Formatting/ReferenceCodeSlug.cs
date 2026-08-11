namespace Recordum.Core.Formatting;

/// <summary>Genera il Code di un'unità/scala custom dal nome (Onda 2.5). Il codice è la chiave di
/// join dei payload template (risolvi-o-crea): minuscolo, [a-z0-9-], mai vuoto, unico rispetto a
/// <paramref name="takenCodes"/> (confronto case-insensitive) con suffisso -2, -3, …</summary>
public static class ReferenceCodeSlug
{
    public static string From(string name, IReadOnlyCollection<string> takenCodes)
    {
        ArgumentNullException.ThrowIfNull(takenCodes);
        var chars = (name ?? string.Empty).Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-');
        var slug = string.Join(null, chars);
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        if (slug.Length == 0) slug = "custom";
        var taken = new HashSet<string>(takenCodes, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(slug)) return slug;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{slug}-{i}")) return $"{slug}-{i}";
    }
}
