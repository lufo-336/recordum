using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitBuilder;

/// <summary>Mattone Obiettivo (Onda 2.5 T4): goal opzionale del draft, offerto solo quando la
/// composizione deriva Counter (metrica numerica o tap counter — il goal di Water intake e' il
/// precedente). Vista sul draft, non chiama servizi (spec §1: host-agnostico); <see cref="Build"/>
/// produce il <see cref="Goal"/> solo con un target valido, altrimenti null (mai un goal a meta').</summary>
public sealed partial class GoalBrickViewModel : ObservableObject
{
    [ObservableProperty] private bool _hasGoal;
    [ObservableProperty] private decimal? _target;

    /// <summary>Nome proprio dell'obiettivo; vuoto = quello dell'abitudine (CR9).</summary>
    [ObservableProperty] private string? _name;

    /// <summary>Il creatore offre <c>Open</c> e <c>Rolling</c>: <c>Fixed</c> vuole inizio e fine e resta in
    /// Configura (<see cref="IsComplete"/> lo rifiuta se arriva comunque).</summary>
    [ObservableProperty] private GoalPeriodType _periodType = GoalPeriodType.Open;

    /// <summary>Giorni della finestra mobile; richiesti e positivi solo con <c>Rolling</c>.</summary>
    [ObservableProperty] private int? _rollingDays;

    /// <summary>Impostato dall'host in <c>Recompute()</c>: true quando <c>DerivedType == Counter</c>.
    /// Non e' un input utente — quando l'host lo spegne, forziamo <see cref="HasGoal"/> a false cosi'
    /// il draft non porta mai un goal orfano se la composizione smette di supportarlo.</summary>
    [ObservableProperty] private bool _isAvailable;

    public event EventHandler? Changed;

    partial void OnHasGoalChanged(bool value) => Raise();
    partial void OnTargetChanged(decimal? value) => Raise();
    partial void OnNameChanged(string? value) => Raise();
    partial void OnPeriodTypeChanged(GoalPeriodType value)
    {
        OnPropertyChanged(nameof(RollingWindowDays));
        Raise();
    }

    partial void OnRollingDaysChanged(int? value)
    {
        OnPropertyChanged(nameof(RollingWindowDays));
        Raise();
    }

    /// <summary>The creator's "Count over the last [N] days" row (Avanzate, Ruling 7): a number makes the goal
    /// <c>Rolling</c> over N days, empty makes it <c>Open</c>. A zero or negative N stays as typed (Rolling, then
    /// <see cref="IsComplete"/> refuses it): what is saved is what is shown. Reads back what a template load wrote; a fixed
    /// period has no window.</summary>
    public int? RollingWindowDays
    {
        get => PeriodType == GoalPeriodType.Rolling ? RollingDays : null;
        set
        {
            PeriodType = value is null ? GoalPeriodType.Open : GoalPeriodType.Rolling;
            RollingDays = value;
        }
    }

    /// <summary>False when the goal is on but cannot be written: no positive target, a rolling window
    /// without its days, or a fixed period (no dates here) — all would violate
    /// <c>ck_goal_period_consistency</c> or mean nothing. Same rule as Configura's
    /// <c>GoalSectionViewModel</c> (<c>Editor.Error.RollingDaysPositive</c>).</summary>
    public bool IsComplete => !HasGoal || Target is > 0 && PeriodType switch
    {
        GoalPeriodType.Rolling => RollingDays is > 0,
        GoalPeriodType.Fixed => false,
        _ => true,
    };

    partial void OnIsAvailableChanged(bool value)
    {
        if (!value) HasGoal = false; // il setter sopra rilancia Changed se il valore cambia davvero
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Torna null se il mattone e' spento o il target manca/non e' positivo — mai un Goal a
    /// meta' composto nella request. <paramref name="habitName"/> e' sempre il nome dell'abitudine
    /// (CR9: mai un literal hardcoded); <paramref name="firstMetricId"/> e' l'id della prima metrica
    /// della composizione, o null per un tap counter puro.</summary>
    public Goal? Build(Guid nodeId, string habitName, Guid? firstMetricId)
    {
        if (!HasGoal || Target is null or <= 0) return null;
        return new Goal
        {
            Id = Guid.NewGuid(),
            HabitNodeId = nodeId,
            Name = string.IsNullOrWhiteSpace(Name) ? habitName : Name.Trim(),
            MetricDefinitionId = firstMetricId,
            TargetValue = Target.Value,
            // Open o Rolling (con i suoi giorni): un periodo senza i campi che lo reggono viola
            // ck_goal_period_consistency e fa fallire l'insert, per questo IsComplete li pretende.
            PeriodType = PeriodType,
            RollingDays = PeriodType == GoalPeriodType.Rolling ? RollingDays : null,
        };
    }
}
