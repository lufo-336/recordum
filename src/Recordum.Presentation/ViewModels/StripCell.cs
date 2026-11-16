namespace Recordum.Presentation.ViewModels;

/// <summary>Una cella della striscia dei giorni: il glifo da scrivere ("✓", "5", "·"), se il giorno ha un valore, se è oggi,
/// se è il giorno scelto nella Home; <see cref="IsLapse"/> = il giorno di una ricaduta ("×", mai <see cref="On"/>).</summary>
public readonly record struct StripCell(string Glyph, bool On, bool IsToday, bool IsSelected = false, bool IsLapse = false);
