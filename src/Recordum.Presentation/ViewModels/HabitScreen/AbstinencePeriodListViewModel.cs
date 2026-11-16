using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Services;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>The Cronologia "Registrazioni" list for a Resistance habit: abstinence periods as rows,
/// open period pinned first then newest-closed first. Deletion (with confirm) removes the row.</summary>
public sealed partial class AbstinencePeriodListViewModel : ObservableObject, IDisposable
{
    private readonly IAbstinenceService _abstinence;
    private readonly IConfirmationDialog _confirm;
    private readonly IClock _clock;
    private readonly ITickSourceFactory? _ticks;

    public ObservableCollection<AbstinencePeriodRowViewModel> Rows { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;

    public event EventHandler? Changed;

    /// <summary>Il confine ricevuto dallo screen: le righe delle registrazioni nominano il giorno
    /// d'inizio e di ricaduta secondo il giorno dell'abitudine, non la data UTC.</summary>
    internal DayBoundary DayBoundary { get; set; } = DayBoundary.Utc;

    /// <summary>B1: il giorno che il blocco del giorno scelto mostra già. I periodi che lo toccano non entrano
    /// nell'elenco di un intervallo.</summary>
    internal DateOnly? ExcludedDay { get; set; }

    /// <summary>Niente da elencare solo perché i periodi stanno tutti in <see cref="ExcludedDay"/>.</summary>
    internal bool OnlyExcludedDayHasPeriods => Rows.Count == 0 && _excludedCount > 0;

    private int _excludedCount;

    public AbstinencePeriodListViewModel(IAbstinenceService abstinence, IConfirmationDialog confirm, IClock clock, ITickSourceFactory? ticks = null)
    {
        _abstinence = abstinence;
        _confirm = confirm;
        _clock = clock;
        _ticks = ticks;
    }

    public Task LoadDayAsync(Guid habitNodeId, DateOnly day, CancellationToken ct = default)
        => LoadRangeAsync(habitNodeId, day, day, ct);

    public async Task LoadRangeAsync(Guid habitNodeId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var skipped = ExcludedDay; // letto prima delle attese: un cambio di giorno a metà non mescola due domande
        var periods = await _abstinence.ListPeriodsAsync(
            habitNodeId, from ?? DateOnly.MinValue, to ?? DateOnly.MaxValue, ct).ConfigureAwait(true);
        var found = periods.Count;
        if (skipped is { } skip)
        {
            // Gli stessi che il blocco del giorno carica con LoadDayAsync: la stessa domanda, non una formula gemella.
            var shown = (await _abstinence.ListPeriodsAsync(habitNodeId, skip, skip, ct).ConfigureAwait(true))
                .Select(p => p.Id).ToHashSet();
            periods = periods.Where(p => !shown.Contains(p.Id)).ToList();
        }
        _excludedCount = found - periods.Count;

        // Dispose the outgoing rows first: the open-period row owns a running tick source that the
        // dispatcher roots, so a bare Clear() would orphan a still-ticking clock on every reload.
        foreach (var r in Rows) r.Dispose();
        Rows.Clear();
        var ordered = periods
            .OrderByDescending(p => p.EndedAtMs is null)          // open first
            .ThenByDescending(p => p.StartedAtMs);                // then newest start
        foreach (var p in ordered)
        {
            var row = new AbstinencePeriodRowViewModel(p, _abstinence, _confirm, _clock, DayBoundary, r => Rows.Remove(r), _ticks);
            row.Changed += (_, _) => { IsEmpty = Rows.Count == 0; Changed?.Invoke(this, EventArgs.Empty); };
            Rows.Add(row);
        }
        IsEmpty = Rows.Count == 0;
    }

    /// <summary>Disposes every row (stopping their live clocks) so no timer survives teardown.</summary>
    public void Dispose()
    {
        foreach (var r in Rows) r.Dispose();
        Rows.Clear();
    }
}
