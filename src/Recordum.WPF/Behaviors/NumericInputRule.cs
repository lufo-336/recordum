using System.Globalization;
using System.Text;
using System.Windows.Controls;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Behaviors;

/// <summary>Refuses numeric input WPF would misread instead of letting it through (W2V-17c).
///
/// <para>The default binding conversion accepts group separators, so the *other* culture's decimal
/// separator is not rejected — it is read as grouping: "2,5" became <c>25</c> under en-US, and "2.5"
/// becomes <c>25</c> once the app runs Italian. A value wrong by a factor of ten, written without a
/// word, is the worst of the three possible outcomes; this rule turns it into a visible refusal.</para>
///
/// <para>Runs at <see cref="ValidationStep.RawProposedValue"/> (the default), i.e. before conversion,
/// which is the only place the raw text is still available. Empty is valid: clearing a field is not
/// an error.</para></summary>
public sealed class NumericInputRule : ValidationRule
{
    /// <summary>Plain numbers only: an optional sign, digits, and at most one decimal separator —
    /// no grouping, which is exactly the ambiguity that produced the tenfold error.</summary>
    private const NumberStyles Allowed =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    private static readonly CompositeFormat MessageFormat = CompositeFormat.Parse(Strings.Input_Number_Invalid);

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var culture = cultureInfo is null || cultureInfo.Equals(CultureInfo.InvariantCulture)
            ? CultureInfo.CurrentCulture
            : cultureInfo;

        var text = value as string ?? value?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return ValidationResult.ValidResult;

        return decimal.TryParse(text, Allowed, culture, out _)
            ? ValidationResult.ValidResult
            : new ValidationResult(false, Message(culture));
    }

    private static string Message(CultureInfo culture)
    {
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        return string.Format(culture, MessageFormat, separator, $"2{separator}5");
    }
}
