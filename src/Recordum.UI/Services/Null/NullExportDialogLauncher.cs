using Recordum.Presentation.ViewModels.Export;

namespace Recordum.UI.Services.Null;

/// <summary>La dialog di export resta con la sua vista al di là di F1 (F6).</summary>
public sealed class NullExportDialogLauncher : IExportDialogLauncher
{
    public Task ShowForHabitAsync(Guid habitNodeId, string habitName) => Task.CompletedTask;

    public Task ShowForLibraryAsync() => Task.CompletedTask;
}
