namespace Recordum.Presentation.Services;

public interface IHabitNavigator
{
    Task OpenHabitAtDayAsync(Guid habitNodeId, string title, DateOnly day);
}
