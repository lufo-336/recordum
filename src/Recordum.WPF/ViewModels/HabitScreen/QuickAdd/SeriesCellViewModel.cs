using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class SeriesCellViewModel : ObservableObject
{
    public MetricDefinition Metric { get; }
    public MetricType Type => Metric.Type;
    public string Name => Metric.Name;

    [ObservableProperty] private long? _integerValue;
    [ObservableProperty] private decimal? _decimalValue;
    [ObservableProperty] private string? _textValue;

    public SeriesCellViewModel(MetricDefinition metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        Metric = metric;
    }

    public CompletionValue? BuildValue(int seriesIndex)
    {
        var v = new CompletionValue
        {
            MetricDefinitionId = Metric.Id,
            SeriesIndex = seriesIndex,
        };
        switch (Metric.Type)
        {
            case MetricType.Integer:
                if (IntegerValue is null) return null;
                v.ValueInteger = IntegerValue;
                break;
            case MetricType.Decimal:
                if (DecimalValue is null) return null;
                v.ValueDecimal = DecimalValue;
                break;
            case MetricType.Seconds:
                if (IntegerValue is null) return null;
                v.ValueInteger = IntegerValue;
                break;
            case MetricType.Text:
                if (string.IsNullOrWhiteSpace(TextValue)) return null;
                v.ValueText = TextValue;
                break;
            default:
                return null;
        }
        return v;
    }
}
