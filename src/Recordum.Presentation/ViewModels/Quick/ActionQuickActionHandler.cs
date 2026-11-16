using System.Globalization;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Quick;

/// <summary>Registra il giorno se non è ancora fatto; se è già fatto chiede conferma e toglie la registrazione
/// più recente di quel giorno (vale solo per l'azione).</summary>
public sealed class ActionQuickActionHandler(
    ICompletionService completions,
    IClock clock,
    IResetTimeResolver resetTimes,
    IConfirmationDialog confirm) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Action;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
    {
        // La conferma apre una finestra modale: restiamo sul thread dell'interfaccia per tutto il flusso.
        var boundary = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(true);
        var today = boundary.Today(clock);
        var target = day ?? today;

        var existing = await CompletionDay.ListAsync(completions, habitNodeId, boundary, target, ct).ConfigureAwait(true);
        if (existing.Count == 0)
        {
            var stamp = CompletionStamp.For(boundary, clock, target);
            var draft = new Completion
            {
                HabitNodeId = habitNodeId,
                CompletedAt = stamp.Timestamp,
                CompletedAtMs = stamp.Ms,
                IsRetroactive = stamp.IsRetroactive,
            };
            await completions.RecordAsync(draft, ct: ct).ConfigureAwait(true);
            return true;
        }

        var label = target == today
            ? Strings.Home_StripHeader_Today
            : CompletionDay.LongLabel(target, CultureInfo.CurrentCulture);
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
        var message = string.Format(CultureInfo.CurrentCulture, Strings.Home_Uncheck_Message, label);
#pragma warning restore CA1863
        var ok = await confirm.ConfirmAsync(Strings.Home_Uncheck_Title, message).ConfigureAwait(true);
        if (ok)
        {
            var latestId = existing.OrderByDescending(c => c.CompletedAtMs).First().Id;
            await completions.DeleteAsync(latestId, ct).ConfigureAwait(true);
        }
        return true;
    }
}
