using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>Gli otto casi della schermata dell'abitudine (spec direzione visiva §6.2): decidono il comando in testa,
/// i quattro numeri e la mappa. Un gruppo non ha un caso.</summary>
public enum HabitCase { Action, CounterTap, Measured, Series, Session, State, Abstinence, Journal }

public static class HabitCases
{
    /// <summary>La stessa scelta di <c>BuildCounterQuickAddAsync</c> e di <c>TimerControlViewModel.IsApplicable</c>,
    /// scritta una volta: il timer vince sui campi; senza campi è il contatore a tocco; un campo solo e non ripetibile è
    /// il misurato; il resto sono serie.</summary>
    public static HabitCase Resolve(HabitType type, HabitConfig? config, int metricCount) => type switch
    {
        HabitType.Resistance => HabitCase.Abstinence,
        HabitType.Journal => HabitCase.Journal,
        HabitType.State => HabitCase.State,
        HabitType.Action => HabitCase.Action,
        _ when config?.HasIntegratedTimer == true && config.TimerMode is not null => HabitCase.Session,
        _ when metricCount == 0 => HabitCase.CounterTap,
        _ when metricCount == 1 && config?.Repeatable != true => HabitCase.Measured,
        _ => HabitCase.Series,
    };
}
