namespace Recordum.Presentation.ViewModels.HabitBuilder;

/// <summary>
/// Abstraction over the WPF builder window so callers (the picker launcher / Home)
/// can open the block-first builder and receive the created node id without WPF coupling.
/// </summary>
public interface IHabitBuilderLauncher
{
    Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default);

    /// <summary>Apre il creatore gia' riempito dal modello <paramref name="templateId"/> ("Personalizza…" e "Usa questo
    /// modello"); torna l'id creato, o null se si annulla o la finestra si chiude.</summary>
    Task<Guid?> LaunchFromTemplateAsync(Guid templateId, Guid? parentId = null, CancellationToken ct = default);
}
