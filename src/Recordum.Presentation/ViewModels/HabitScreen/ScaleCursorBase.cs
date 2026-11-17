using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;
using Recordum.Core.Scales;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>Il cursore di registrazione di una scala (GIO-23 §7): slider da Min a Max col passo della precisione, campo
/// numerico per il valore esatto, lettura estesa e tacche. Lo condividono la riga "Oggi" dello Stato e la cella di scala
/// del quick-add: registrano il valore vero (<see cref="CurrentValue"/>), mai una posizione.</summary>
public abstract partial class ScaleCursorBase : ObservableObject
{
    private const double FallbackMax = 10;
    private bool _sync;

    [ObservableProperty] private double _min;
    [ObservableProperty] private double _max = FallbackMax;
    [ObservableProperty] private double _step = 1;
    [ObservableProperty] private double _sliderValue;
    [ObservableProperty] private string _valueText = "0";
    [ObservableProperty] private string _currentLabel = "0";

    /// <summary>La scala del cursore; null = nessuna (slider numerico 0–10 che non registra).</summary>
    protected ScaleTemplate? Scale { get; private set; }

    /// <summary>Gli scaglioni con nome, più una tacca per intero se la scala ha al più 12 valori.</summary>
    public IReadOnlyList<ScalePoint> Points { get; private set; } = [];

    /// <summary>Il valore che si registrerebbe adesso: quello dello slider, tagliato e arrotondato dalla scala.</summary>
    protected decimal CurrentValue => Scale is null
        ? (decimal)SliderValue
        : ScaleRules.Normalize((decimal)SliderValue, Scale.MinValue, Scale.MaxValue, Scale.Precision);

    /// <summary>Imposta (o toglie) la scala. Min e Max prima del valore, così non viene mai coartato.</summary>
    protected void UseScale(ScaleTemplate? scale)
    {
        Scale = scale;
        if (scale is null)
        {
            Min = 0; Max = FallbackMax; Step = 1; Points = [];
        }
        else
        {
            Min = scale.MinValue;
            Max = scale.MaxValue;
            Step = scale.Precision == Core.Enums.ScalePrecision.Decimal ? 0.01 : 1;
            Points = BuildPoints(scale);
        }
        OnPropertyChanged(nameof(Points));
        SetValue(Min);
    }

    /// <summary>Porta il cursore su un valore (già normalizzato o no) e riallinea testo ed etichetta.</summary>
    protected void SetValue(double value)
    {
        SliderValue = value;
        Refresh();
    }

    partial void OnSliderValueChanged(double value) => Refresh();

    partial void OnValueTextChanged(string value)
    {
        if (_sync) return;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
        {
            var v = Scale is null ? parsed : ScaleRules.Normalize(parsed, Scale.MinValue, Scale.MaxValue, Scale.Precision);
            SliderValue = (double)v;
        }
        Refresh();   // un testo non valido riporta quello di prima
    }

    private void Refresh()
    {
        _sync = true;
        try
        {
            var v = CurrentValue;
            ValueText = Scale is null ? v.ToString(CultureInfo.CurrentCulture) : ScaleText.Number(Scale, v);
            CurrentLabel = Scale is null ? ValueText : ScaleText.Long(Scale, v);
        }
        finally { _sync = false; }
    }

    internal static List<ScalePoint> BuildPoints(ScaleTemplate scale)
    {
        var values = ScaleText.Named(scale).Select(a => a.Value).ToHashSet();
        if (scale.MaxValue - scale.MinValue + 1 <= ScaleRules.TickLimit)
            for (var v = scale.MinValue; v <= scale.MaxValue; v++) values.Add(v);
        return values.Order()
            .Select(v => new ScalePoint(ScaleText.Short(scale, v), ScaleText.Fraction(scale, v), v))
            .ToList();
    }
}
