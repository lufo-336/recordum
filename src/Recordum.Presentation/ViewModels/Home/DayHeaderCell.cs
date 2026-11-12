namespace Recordum.Presentation.ViewModels.Home;

/// <summary>Un giorno dell'intestazione della Home: il nome breve ("lun"; "oggi" per oggi), l'iniziale, il giorno lungo
/// ("lunedì 9 novembre") e quanti giorni fa è (0 = oggi).</summary>
public readonly record struct DayHeaderCell(string ShortName, string Initial, string FullName, int DaysBack, bool IsToday, bool IsSelected);
