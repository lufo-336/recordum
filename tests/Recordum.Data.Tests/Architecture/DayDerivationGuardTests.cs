using FluentAssertions;
using Xunit;

namespace Recordum.Data.Tests.Architecture;

/// <summary>
/// Il gemello del guard di R7 e di BuilderTemplateParityTests: la tabella §0 del piano R9 elenca ogni
/// punto che deriva un giorno da un istante, e senza qualcuno che la rilegga quella tabella scade al
/// primo merge. Questo test fallisce se ricompare una derivazione UTC fuori whitelist.
/// </summary>
public class DayDerivationGuardTests
{
    /// <summary>I soli siti a cui è lecito derivare un giorno senza <c>DayBoundary</c>, con il perché.
    /// Aggiungerne uno è una decisione: si legge il sito, si scrive la ragione, e si aggiorna la
    /// tabella §0 del piano R9. Se un sito qui dentro dovrebbe convertire, è un pezzo dimenticato.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Recordum.Core/Time/DayBoundary.cs"] =
            "è l'implementazione della regola: qui la derivazione si definisce, non si consuma",
        ["Recordum.Core/Time/NoteAnchor.cs"] =
            "solo la menzione dell'espressione vecchia dentro la doc, per dire da cosa viene",
        ["Recordum.Data/Maintenance/RetroactiveNoonBackfill.cs"] =
            "il giorno DICHIARATO da una riga già scritta va riletto come fu scritto (Task 11)",
        ["Recordum.WPF/Converters/DateOnlyToDateTimeConverter.cs"] =
            "presentazione: converte il DateTime del DatePicker, non un istante (A21/B19)",
        ["Recordum.Presentation/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelBase.cs"] =
            "CompletionDate è il giorno che l'utente ha scelto sul picker, non un istante da bucketizzare",
        ["Recordum.Presentation/ViewModels/HabitScreen/QuickAdd/JournalQuickAddViewModel.cs"] =
            "CompletionDate.Date è già un giorno di dominio scelto dall'utente, non un istante (A49)",
    };

    [Fact]
    public void No_source_file_derives_a_day_from_an_instant_outside_DayBoundary()
    {
        var root = FindRepoRoot();
        var srcRoot = Path.Combine(root, "src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                || file.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
                || file.Contains($"{sep}Migrations{sep}", StringComparison.Ordinal)) continue;

            var relative = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (Allowed.ContainsKey(relative)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("DateOnly.FromDateTime", StringComparison.Ordinal)
                    || line.Contains("UtcNow.UtcDateTime.Date", StringComparison.Ordinal))
                    offenders.Add($"{relative}:{i + 1}  {line.Trim()}");
            }
        }

        offenders.Should().BeEmpty(
            "il giorno di appartenenza si deriva solo da DayBoundary (spec R9). "
            + "Se questa derivazione è legittima — presentazione, o un DateOnly già di dominio — "
            + "aggiungerla alla whitelist con il perché, e aggiornare la tabella §0 del piano R9.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Recordum.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Recordum.slnx non trovato risalendo da " + AppContext.BaseDirectory);
    }
}
