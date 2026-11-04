using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;

namespace Recordum.Presentation.ViewModels.HabitScreen.Figures;

/// <summary>I numeri della testa di un gruppo: giorni completi di fila, % a 30 giorni, l'obiettivo del gruppo e la figlia
/// più saltata. Un giorno è completo quando ogni figlia prevista quel giorno è fatta; un giorno senza figlie previste non
/// conta. I giorni previsti e fatti li dà <see cref="IGroupHistoryQueryService"/> (già nel confine di ciascuna figlia).</summary>
public sealed partial class GroupFiguresViewModel(IGroupHistoryQueryService history, IClock clock) : ObservableObject
{
    private const int Window = 30;
    private int _generation;

    [ObservableProperty] private ImmutableArray<HabitFigure> _items = [];

    /// <summary>L'inizio del gruppo (Ruling 4) dell'ultimo caricamento: il più vecchio fra la creazione del gruppo e
    /// l'inizio delle figlie con giorni previsti. La schermata lo usa per "Tutto".</summary>
    public DateOnly? StartDay { get; private set; }

    public async Task LoadAsync(HabitNode group, DayBoundary day, GoalProgressCardViewModel goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(goal);
        var generation = ++_generation;
        var today = day.Today(clock);
        var created = day.DayOf(group.CreatedAt);
        if (created > today) created = today;

        var children = await history.GetAsync(group.Id, created, today, ct).ConfigureAwait(true);
        // Una figlia nata prima del gruppo (spostata dentro): i suoi giorni vecchi non erano nella finestra, si rilegge.
        var begin = children.Where(c => c.Scheduled.Count > 0).Select(c => c.StartsOn).DefaultIfEmpty(created).Min();
        if (begin < created)
        {
            created = begin;
            children = await history.GetAsync(group.Id, created, today, ct).ConfigureAwait(true);
        }

        var runs = new List<(DateOnly Day, bool Complete)>();   // solo i giorni con almeno una figlia prevista
        for (var d = created; d <= today; d = d.AddDays(1))
        {
            var due = children.Where(c => c.Scheduled.Contains(d)).ToList();
            if (due.Count > 0) runs.Add((d, due.All(c => c.Done.Contains(d))));
        }

        var run = 0;
        for (var i = runs.Count - 1; i >= 0; i--)
        {
            if (runs[i].Complete) run++;
            else if (runs[i].Day != today) break;   // la giornata è aperta: oggi incompleto non interrompe
        }

        var start = Max(today.AddDays(-(Window - 1)), created);
        var inWindow = runs.Where(r => r.Day >= start).ToList();
        var pct = inWindow.Count == 0 ? 0
            : (int)Math.Round(100.0 * inWindow.Count(r => r.Complete) / inWindow.Count, MidpointRounding.AwayFromZero);

        var figures = new List<HabitFigure>
        {
            new(Int(run), Strings.Group_Figures_CompleteRun),
            new(string.Create(CultureInfo.CurrentCulture, $"{pct}%"), Strings.Figures_Pct30),
        };
        if (!goal.IsEmpty)
            figures.Add(new(Of(Dec(goal.Actual), Dec(goal.Target)),
                goal.Name.Length > 0 ? goal.Name : Strings.Group_Figures_Goal));

        // Oggi escluso: la giornata è aperta, non è ancora "saltata".
        var skipped = children
            .Select(c => (Child: c, Misses: c.Scheduled.Count(d => d >= start && d < today && !c.Done.Contains(d))))
            .Where(x => x.Misses > 0).OrderByDescending(x => x.Misses).FirstOrDefault();
        if (skipped.Child is { } worst) figures.Add(new(worst.Name, Strings.Group_Figures_MostSkipped));

        if (generation != _generation) return;
        StartDay = created;
        Items = [.. figures];
    }

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI strings
    private static string Of(string a, string b) => string.Format(CultureInfo.CurrentCulture, Strings.Home_Group_Progress, a, b);
#pragma warning restore CA1863

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static string Int(int n) => n.ToString("N0", CultureInfo.CurrentCulture);
    private static string Dec(decimal d) => Math.Round(d, 1, MidpointRounding.AwayFromZero).ToString("#,0.#", CultureInfo.CurrentCulture);
}
