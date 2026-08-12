using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Mattone Obiettivo (Onda 2.5 T4): goal opzionale del draft, offerto solo quando la
/// composizione deriva Counter (metrica numerica o tap counter — il goal di Water intake e' il
/// precedente). Vista sul draft, non chiama servizi (spec §1: host-agnostico); <see cref="Build"/>
/// produce il <see cref="Goal"/> solo con un target valido, altrimenti null (mai un goal a meta').</summary>
public sealed partial class GoalBrickViewModel : ObservableObject
{
    [ObservableProperty] private bool _hasGoal;
    [ObservableProperty] private decimal? _target;

    /// <summary>Impostato dall'host in <c>Recompute()</c>: true quando <c>DerivedType == Counter</c>.
    /// Non e' un input utente — quando l'host lo spegne, forziamo <see cref="HasGoal"/> a false cosi'
    /// il draft non porta mai un goal orfano se la composizione smette di supportarlo.</summary>
    [ObservableProperty] private bool _isAvailable;

    public event EventHandler? Changed;

    partial void OnHasGoalChanged(bool value) => Raise();
    partial void OnTargetChanged(decimal? value) => Raise();

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
            Name = habitName,
            MetricDefinitionId = firstMetricId,
            TargetValue = Target.Value,
            // Sempre aperto: v. il commento in HabitBuilderView.xaml (review 2.5, F1). Un periodo
            // Fixed/Rolling senza date o rolling_days viola ck_goal_period_consistency e fa fallire
            // l'insert; il periodo si sceglie in Configura, che ha i campi per reggerlo.
            PeriodType = GoalPeriodType.Open,
        };
    }
}
