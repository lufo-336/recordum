namespace Recordum.Plugins.Contracts.Events;

public static class WellKnownEvents
{
    public static readonly EventName HabitCreated = new("habit.created");
    public static readonly EventName HabitUpdated = new("habit.updated");
    public static readonly EventName HabitDeleted = new("habit.deleted");

    public static readonly EventName CompletionRecorded = new("completion.recorded");
    public static readonly EventName CompletionUpdated = new("completion.updated");
    public static readonly EventName CompletionDeleted = new("completion.deleted");

    public static readonly EventName NoteCreated = new("note.created");
    public static readonly EventName NoteUpdated = new("note.updated");

    public static readonly EventName StreakMilestone = new("streak.milestone");
    public static readonly EventName GoalCompleted = new("goal.completed");

    public static readonly EventName AppThemeChanged = new("app.theme.changed");
    public static readonly EventName AppLocaleChanged = new("app.locale.changed");
}
