using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Quick-add del counter NUDO (zero metriche): ogni tap = un'occorrenza registrata subito;
/// il conteggio del giorno è il numero di completion. ⌫ annulla l'ultima del giorno. Spec multivalue §4.1.</summary>
public sealed partial class CounterTapQuickAddViewModel(ICompletionService completions, IClock clock)
    : QuickAddViewModelBase(completions, clock)
{
    [ObservableProperty] private int _dayCount;

    /// <summary>Conta le occorrenze del giorno selezionato (CompletionDate, anche retroattivo).</summary>
    public async Task LoadDayAsync(CancellationToken ct = default)
    {
        if (Node is null) { DayCount = 0; return; }
        var (from, to) = DayWindowUtc();
        var list = await Completions.ListByHabitAsync(Node.Id, from, to, ct: ct).ConfigureAwait(true);
        DayCount = list.Count;
        UndoLastCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task Tap(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        // AsyncRelayCommand.ExecuteAsync non consulta CanExecute: la guardia del doppio clic sta qui.
        if (Node is null || IsSaving) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var (ts, ms, retro) = ResolveCompletionTimestamp();
            await Completions.RecordAsync(new Completion
            {
                HabitNodeId = Node.Id, CompletedAt = ts, CompletedAtMs = ms, IsRetroactive = retro,
            }, ct: ct).ConfigureAwait(true);
            // Si rilegge, non si incrementa: l'evento CompletionRecorded fa già rileggere lo schermo, e un ++ dopo la
            // sua rilettura contava due volte la stessa registrazione.
            await LoadDayAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex) { SetUnexpectedError(ex); }
        finally { IsSaving = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoLast(CancellationToken ct)
    {
        if (Node is null) return;
        var (from, to) = DayWindowUtc();
        var list = await Completions.ListByHabitAsync(Node.Id, from, to, ct: ct).ConfigureAwait(true);
        var newest = list.OrderByDescending(c => c.CompletedAtMs).FirstOrDefault();
        if (newest is null) return;
        await Completions.DeleteAsync(newest.Id, ct).ConfigureAwait(true);
        await LoadDayAsync(ct).ConfigureAwait(true);
    }

    private bool CanUndo() => DayCount > 0;

    /// <summary>Il giorno di <see cref="QuickAddViewModelBase.CompletionDate"/> nel confine dell'abitudine. RangeOf è
    /// semiaperta, il filtro del servizio inclusivo al millisecondo: -1 ms.</summary>
    private (DateTimeOffset From, DateTimeOffset To) DayWindowUtc()
    {
        var (from, toExclusive) = DayBoundary.RangeOf(CompletionDay);
        return (from, toExclusive.AddMilliseconds(-1));
    }
}
