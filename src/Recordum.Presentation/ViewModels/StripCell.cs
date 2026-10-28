namespace Recordum.Presentation.ViewModels;

/// <summary>Una cella della striscia dei giorni: il glifo da scrivere ("✓", "5", "·"), se il giorno ha un valore, se è oggi.
/// Serve anche all'intestazione della Home, dove il glifo è il nome breve del giorno.</summary>
public readonly record struct StripCell(string Glyph, bool On, bool IsToday);
