using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.HabitBuilder;

/// <summary>Un campo misurato nella composizione. La 'nature' determina i sotto-campi rilevanti
/// (unità per Quantity, scala per Scale). Possiede un Id client-side per il MetricDefinition futuro.</summary>
public sealed partial class FieldBlockViewModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public FieldNature Nature { get; }

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetricType))]
    private Guid? _unitOfMeasureId;     // Quantity (opzionale)

    [ObservableProperty] private Guid? _scaleTemplateId;     // Scale

    /// <summary>Quantity-only: does this field accept fractions? A unit implies it (2.5 l), but a
    /// unit-less quantity can want them too — body weight is the seed's own example (Onda 2.5 T10:
    /// the parity guard found the builder could not compose "Workout log"'s Weight metric, a Decimal
    /// with no unit). Off by default: counts are the common case.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetricType))]
    private bool _allowsDecimals;

    public FieldBlockViewModel(FieldNature nature, string name)
    {
        Nature = nature;
        _name = name;
    }

    /// <summary>Mappa la nature al MetricType del dominio. Per Quantity: Decimal se c'è un'unità o se
    /// l'utente ha chiesto i decimali, altrimenti Integer.</summary>
    public MetricType MetricType => Nature switch
    {
        FieldNature.Quantity => UnitOfMeasureId is null && !AllowsDecimals ? MetricType.Integer : MetricType.Decimal,
        FieldNature.Time => MetricType.Seconds,
        FieldNature.Scale => MetricType.Integer,
        FieldNature.Text => MetricType.Text,
        _ => MetricType.Integer,
    };
}
