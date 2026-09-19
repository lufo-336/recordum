using Recordum.Presentation.ViewModels.Export;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeExportDialogLauncher : IExportDialogLauncher
{
    public int HabitCount { get; private set; }
    public int LibraryCount { get; private set; }
    public Guid? LastHabitId { get; private set; }
    public string? LastHabitName { get; private set; }

    public Task ShowForHabitAsync(Guid habitNodeId, string habitName)
    {
        HabitCount++;
        LastHabitId = habitNodeId;
        LastHabitName = habitName;
        return Task.CompletedTask;
    }

    public Task ShowForLibraryAsync()
    {
        LibraryCount++;
        return Task.CompletedTask;
    }
}
