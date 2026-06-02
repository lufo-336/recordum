using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Un campo misurato nella composizione. La 'nature' determina i sotto-campi rilevanti
/// (unità per Quantity, scala per Scale). Possiede un Id client-side per il MetricDefinition futuro.</summary>
public sealed partial class FieldBlockViewModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public FieldNature Nature { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private Guid? _unitOfMeasureId;     // Quantity
    [ObservableProperty] private Guid? _scaleTemplateId;     // Scale

    public FieldBlockViewModel(FieldNature nature, string name)
    {
        Nature = nature;
        _name = name;
    }

    /// <summary>Mappa la nature al MetricType del dominio.</summary>
    public MetricType MetricType => Nature switch
    {
        FieldNature.Count => MetricType.Integer,
        FieldNature.Quantity => MetricType.Decimal,
        FieldNature.Time => MetricType.Seconds,
        FieldNature.Scale => MetricType.Integer,
        FieldNature.Text => MetricType.Text,
        _ => MetricType.Integer,
    };
}
