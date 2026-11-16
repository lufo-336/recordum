using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using DayWindow = Recordum.Presentation.Services.CompletionDay;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

public sealed partial class ActionQuickAddViewModel(ICompletionService completions, IClock clock)
    : QuickAddViewModelBase(completions, clock)
{
    /// <summary>Il giorno di <see cref="QuickAddViewModelBase.CompletionDate"/> ha già una registrazione (il comando in
    /// testa è un interruttore: "Fatto oggi" / togli).</summary>
    [ObservableProperty] private bool _isDone;

    /// <summary>Rilegge <see cref="IsDone"/> per il giorno di <see cref="QuickAddViewModelBase.CompletionDate"/>, nella
    /// finestra del confine dell'abitudine.</summary>
    public async Task LoadDayAsync(CancellationToken ct = default)
        => IsDone = Node is not null && (await ListDayAsync(ct).ConfigureAwait(true)).Count > 0;

    /// <summary>Registra se il giorno non è fatto; altrimenti cancella la registrazione più recente del giorno.</summary>
    [RelayCommand]
    private async Task ToggleAsync(CancellationToken ct)
    {
        // AsyncRelayCommand.ExecuteAsync non consulta CanExecute: la guardia del doppio clic sta qui.
        if (Node is null || IsSaving) return;
        var day = await ListDayAsync(ct).ConfigureAwait(true);
        if (day.Count == 0)
        {
            await SaveAsync(ct).ConfigureAwait(true);
        }
        else
        {
            try { await Completions.DeleteAsync(day.OrderByDescending(c => c.CompletedAtMs).First().Id, ct).ConfigureAwait(true); }
            catch (Exception ex) { SetUnexpectedError(ex); }
        }
        await LoadDayAsync(ct).ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<Completion>> ListDayAsync(CancellationToken ct)
        => await DayWindow.ListAsync(Completions, Node!.Id, DayBoundary, CompletionDay, ct).ConfigureAwait(true);

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        // AsyncRelayCommand.ExecuteAsync non consulta CanExecute: la guardia del doppio clic sta qui.
        if (Node is null || IsSaving) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var (ts, ms, retro) = ResolveCompletionTimestamp();
            var draft = new Completion
            {
                HabitNodeId = Node.Id,
                CompletedAt = ts,
                CompletedAtMs = ms,
                IsRetroactive = retro,
            };
            await Completions.RecordAsync(draft, ct: ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
