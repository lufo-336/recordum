using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>La heatmap e la mappa sono nel colore dell'abitudine (direzione visiva §4.3): i verdi di GitHub e le loro
/// chiavi non devono tornare, nemmeno per comodità in una vista nuova.</summary>
public class NoGitHubGreensTests
{
    private static readonly string[] Forbidden = ["Brush.Heatmap", "#9BE9A8", "#40C463", "#30A14E", "#216E39", "#0E4429", "#006D32", "#26A641", "#39D353"];

    [Fact]
    public void No_source_of_the_shell_uses_the_GitHub_greens()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Recordum.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var ui = Path.Combine(dir!.FullName, "src", "Recordum.UI");
        var hits = Directory.EnumerateFiles(ui, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".axaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => Forbidden.Where(k => File.ReadAllText(f).Contains(k, StringComparison.OrdinalIgnoreCase))
                .Select(k => $"{Path.GetRelativePath(ui, f)}: {k}"))
            .ToList();
        Assert.True(hits.Count == 0, string.Join(Environment.NewLine, hits));
    }
}
