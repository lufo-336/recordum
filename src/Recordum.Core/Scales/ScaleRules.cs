using Recordum.Core.Enums;

namespace Recordum.Core.Scales;

public static class ScaleRules
{
    public const int Bound = 100;
    public const int TickLimit = 12;
    public const int Decimals = 2;

    /// <summary>Codici d'errore della forma di una scala; vuota = valida.</summary>
    public static IReadOnlyList<string> ShapeErrors(int min, int max, IReadOnlyList<int> anchorValues)
    {
        var errors = new List<string>();
        if (min < -Bound || max > Bound) errors.Add("range");
        if (min >= max) errors.Add("order");
        if (anchorValues.Any(v => v < min || v > max)) errors.Add("anchor-outside");
        if (anchorValues.Distinct().Count() != anchorValues.Count) errors.Add("anchor-duplicate");
        return errors;
    }

    /// <summary>Codici d'errore di una modifica rispetto ai valori già registrati.</summary>
    public static IReadOnlyList<string> EditErrors(int newMin, int newMax, ScalePrecision newPrecision,
        decimal? recordedMin, decimal? recordedMax, bool recordedHasDecimals)
    {
        var errors = new List<string>();
        if (recordedMin < newMin) errors.Add("narrow-min");
        if (recordedMax > newMax) errors.Add("narrow-max");
        if (newPrecision == ScalePrecision.Integer && recordedHasDecimals) errors.Add("precision");
        return errors;
    }

    public static decimal Normalize(decimal v, int min, int max, ScalePrecision p)
        => Math.Round(Math.Clamp(v, min, max), p == ScalePrecision.Decimal ? Decimals : 0, MidpointRounding.AwayFromZero);
}
