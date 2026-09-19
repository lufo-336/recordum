namespace Recordum.Presentation.ViewModels.HabitBuilder;

/// <summary>
/// Abstraction over the WPF builder window so callers (the picker launcher / Home)
/// can open the block-first builder and receive the created node id without WPF coupling.
/// </summary>
public interface IHabitBuilderLauncher
{
    Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default);
}
