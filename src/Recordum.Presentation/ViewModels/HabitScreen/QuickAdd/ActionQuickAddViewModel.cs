using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

public sealed partial class ActionQuickAddViewModel(ICompletionService completions, IClock clock)
    : QuickAddViewModelBase(completions, clock)
{
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
