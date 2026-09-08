using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Services;
using Recordum.Core.Time;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>The Cronologia "Registrazioni" list for a Resistance habit: abstinence periods as rows,
/// open period pinned first then newest-closed first. Deletion (with confirm) removes the row.</summary>
public sealed partial class AbstinencePeriodListViewModel : ObservableObject, IDisposable
{
    private readonly IAbstinenceService _abstinence;
    private readonly IConfirmationDialog _confirm;
    private readonly IClock _clock;

    public ObservableCollection<AbstinencePeriodRowViewModel> Rows { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;

    public event EventHandler? Changed;

    /// <summary>Il confine ricevuto dallo screen: le righe delle registrazioni nominano il giorno
    /// d'inizio e di ricaduta secondo il giorno dell'abitudine, non la data UTC.</summary>
    internal DayBoundary DayBoundary { get; set; } = DayBoundary.Utc;

    public AbstinencePeriodListViewModel(IAbstinenceService abstinence, IConfirmationDialog confirm, IClock clock)
    {
        _abstinence = abstinence;
        _confirm = confirm;
        _clock = clock;
    }

    public Task LoadDayAsync(Guid habitNodeId, DateOnly day, CancellationToken ct = default)
        => LoadRangeAsync(habitNodeId, day, day, ct);

    public async Task LoadRangeAsync(Guid habitNodeId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var periods = await _abstinence.ListPeriodsAsync(
            habitNodeId, from ?? DateOnly.MinValue, to ?? DateOnly.MaxValue, ct).ConfigureAwait(true);

        // Dispose the outgoing rows first: the open-period row owns a running DispatcherTimer that the
        // dispatcher roots, so a bare Clear() would orphan a still-ticking clock on every reload.
        foreach (var r in Rows) r.Dispose();
        Rows.Clear();
        var ordered = periods
            .OrderByDescending(p => p.EndedAtMs is null)          // open first
            .ThenByDescending(p => p.StartedAtMs);                // then newest start
        foreach (var p in ordered)
        {
            var row = new AbstinencePeriodRowViewModel(p, _abstinence, _confirm, _clock, DayBoundary, r => Rows.Remove(r));
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
