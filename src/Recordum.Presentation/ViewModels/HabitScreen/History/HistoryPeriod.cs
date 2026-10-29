namespace Recordum.Presentation.ViewModels.HabitScreen.History;

/// <summary>I periodi della Cronologia (spec direzione visiva §6.1). Tipo nuovo: <c>EntriesPeriod</c> lo legge WPF.</summary>
public enum HistoryPeriod { Week, Month, ThreeMonths, Year, All, Custom }

/// <summary>La forma della mappa per un periodo.</summary>
public enum MapShape { Week, Month, Heatmap, YearHeatmaps }
