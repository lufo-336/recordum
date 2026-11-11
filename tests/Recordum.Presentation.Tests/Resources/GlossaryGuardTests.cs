using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Recordum.Presentation.Tests.Resources;

/// <summary>Guardia del glossario (Onda Giornata, Task 1): le parole vietate dall'autore non devono comparire
/// nei file .resx di Strings. Ogni test fallisce elencando 'chiave: valore', una violazione per riga.</summary>
public sealed class GlossaryGuardTests
{
    private static readonly string[] s_italianForbiddenWords =
    [
        "streak",
        "resistenza",
        "resistere",
        "journal",
        "pulit",
        "ho ceduto",
        "gesto primario"
    ];

    private static readonly string[] s_englishForbiddenWords =
    [
        "resistance",
        "clean",
        "gave in",
        "attempt"
    ];

    [Fact]
    public void Italian_values_avoid_forbidden_words()
    {
        var italian = LoadResx("Strings.it.resx");
        var violations = new List<string>();

        foreach (var (key, value) in italian)
        {
            var hasForbiddenWord = s_italianForbiddenWords.Any(w => value.Contains(w, StringComparison.OrdinalIgnoreCase));
            var hasStreakSerie = key.Contains("Streak", StringComparison.OrdinalIgnoreCase)
                && value.Contains("serie", StringComparison.OrdinalIgnoreCase);
            var hasAbstinenceTentativ = (key.Contains("Abstinence", StringComparison.OrdinalIgnoreCase) || key.Contains("Resistance", StringComparison.OrdinalIgnoreCase))
                && value.Contains("tentativ", StringComparison.OrdinalIgnoreCase);

            if (hasForbiddenWord || hasStreakSerie || hasAbstinenceTentativ)
            {
                violations.Add($"{key}: {value}");
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, violations));
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void English_values_avoid_forbidden_words()
    {
        var english = LoadResx("Strings.resx");
        var violations = new List<string>();

        foreach (var (key, value) in english)
        {
            var isTargetKey = key.Contains("Abstinence", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Resistance", StringComparison.OrdinalIgnoreCase)
                || key.Contains("HabitType", StringComparison.OrdinalIgnoreCase);

            if (isTargetKey && s_englishForbiddenWords.Any(w => value.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"{key}: {value}");
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, violations));
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Every_key_exists_in_both_languages()
    {
        var english = LoadResx("Strings.resx");
        var italian = LoadResx("Strings.it.resx");

        var missingInItalian = english.Keys
            .Where(k => !italian.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => $"{k}: (missing in Strings.it.resx)");

        var missingInEnglish = italian.Keys
            .Where(k => !english.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => $"{k}: (missing in Strings.resx)");

        var violations = missingInItalian.Concat(missingInEnglish).ToList();

        if (violations.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, violations));
        }

        violations.Should().BeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Recordum.Presentation", "Resources", "Strings.resx");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Recordum.Presentation", "Resources", "Strings.resx");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate repository root containing src/Recordum.Presentation/Resources/Strings.resx");
    }

    private static Dictionary<string, string> LoadResx(string fileName)
    {
        var repoRoot = FindRepositoryRoot();
        var fullPath = Path.Combine(repoRoot, "src", "Recordum.Presentation", "Resources", fileName);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var doc = XDocument.Load(stream);

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (doc.Root != null)
        {
            foreach (var dataElement in doc.Root.Elements("data"))
            {
                var name = dataElement.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    dict[name] = dataElement.Element("value")?.Value ?? string.Empty;
                }
            }
        }

        return dict;
    }
}
