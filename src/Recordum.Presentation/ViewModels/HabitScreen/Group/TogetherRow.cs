using Recordum.Presentation.ViewModels.HabitScreen.History;

namespace Recordum.Presentation.ViewModels.HabitScreen.Group;

/// <summary>Una figlia nella vista "Insieme": una cella per giorno del periodo, nel colore della figlia.</summary>
public sealed record TogetherRow(Guid HabitNodeId, string Name, string ColorHex, int Depth, IReadOnlyList<HistoryDayCell> Cells);
