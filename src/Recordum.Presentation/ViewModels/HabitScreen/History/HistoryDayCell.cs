using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.Presentation.ViewModels.HabitScreen.History;

/// <summary>Un giorno della mappa. <see cref="Step"/> 0–4 è il gradino del colore dell'abitudine; <see cref="IsLapse"/> è
/// una ricaduta (cella vuota col contorno e una ×, non un giorno rosso né pulito); <see cref="InRange"/> falso = fuori mese o fuori periodo.</summary>
public sealed partial class HistoryDayCell(DateOnly date, bool inRange, bool isToday, bool isFuture) : ObservableObject
{
    public DateOnly Date { get; } = date;
    public int DayNumber => Date.Day;
    public bool InRange { get; } = inRange;
    public bool IsToday { get; } = isToday;
    public bool IsFuture { get; } = isFuture;

    [ObservableProperty] private int _step;
    [ObservableProperty] private bool _isLapse;
    [ObservableProperty] private int _noteCount;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string? _value;

    public bool HasNote => NoteCount > 0;

    partial void OnNoteCountChanged(int value) => OnPropertyChanged(nameof(HasNote));
}

/// <summary>Una heatmap d'anno della forma "Tutto": colonne di settimana.</summary>
public sealed record HistoryYear(int Year, IReadOnlyList<IReadOnlyList<HistoryDayCell>> Weeks);
