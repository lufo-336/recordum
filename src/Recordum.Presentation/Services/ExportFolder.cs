using Recordum.Plugins.Contracts.Export;

namespace Recordum.Presentation.Services;

/// <summary>Decide dove finisce un export con file accessori: la cartella <c>&lt;nome&gt;/</c> accanto al percorso
/// scelto (<c>&lt;nome&gt; (2)</c>, <c>(3)</c>… se esiste già), il documento dentro, ogni file al suo percorso relativo.</summary>
public static class ExportFolder
{
    /// <summary>Tutte le scritture da fare, validate prima di farne una: un percorso relativo assoluto o con
    /// <c>..</c> lancia <see cref="InvalidDataException"/> e non si scrive niente.</summary>
    public static IReadOnlyList<(string Path, byte[] Content)> Plan(string chosenPath, ExportOutput output, Func<string, bool> directoryExists)
    {
        foreach (var f in output.Files)
        {
            var parts = f.RelativePath.Split('/', '\\');
            if (Path.IsPathRooted(f.RelativePath) || parts.Contains("..") || f.RelativePath.StartsWith('/') || parts.Contains(""))
                throw new InvalidDataException($"Unsafe export path: '{f.RelativePath}'.");
        }

        var dir = Path.GetDirectoryName(chosenPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(chosenPath);
        var folder = Path.Combine(dir, name);
        for (var n = 2; directoryExists(folder); n++) folder = Path.Combine(dir, $"{name} ({n})");

        var plan = new List<(string, byte[])> { (Path.Combine(folder, Path.GetFileName(chosenPath)), output.Document) };
        plan.AddRange(output.Files.Select(f => (Path.Combine(folder, Path.Combine(f.RelativePath.Split('/', '\\'))), f.Content)));
        return plan;
    }
}
