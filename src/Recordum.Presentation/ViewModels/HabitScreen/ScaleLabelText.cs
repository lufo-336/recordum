using System.Globalization;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>L'etichetta di un gradino di scala letta per posizione. Un'etichetta vuota (una scala con i buchi, salvata da
/// Configura) o mancante ricade sul numero del gradino (1-based), mai su una stringa vuota.</summary>
internal static class ScaleLabelText
{
    public static string At(IReadOnlyList<string> labels, int index) =>
        index >= 0 && index < labels.Count && !string.IsNullOrWhiteSpace(labels[index])
            ? labels[index]
            : (index + 1).ToString(CultureInfo.CurrentCulture);
}
