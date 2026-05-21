namespace Recordum.WPF.ViewModels.HabitEditor;

/// <summary>
/// Abstraction over the WPF picker modal so HomeViewModel can be unit-tested without WPF.
/// </summary>
public interface IHabitTemplatePickerLauncher
{
    Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default);
}
