namespace Recordum.WPF.ViewModels.Export;

/// <summary>Abstraction over the WPF export modal so VMs can request it without referencing the window.</summary>
public interface IExportDialogLauncher
{
    Task ShowForHabitAsync(Guid habitNodeId, string habitName);
    Task ShowForLibraryAsync();
}
