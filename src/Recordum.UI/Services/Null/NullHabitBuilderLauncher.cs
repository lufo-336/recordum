using Recordum.Presentation.ViewModels.HabitBuilder;

namespace Recordum.UI.Services.Null;

/// <summary>Il builder "Crea nuova abitudine" resta con la sua vista al di là di F1. "Annullato" per
/// un launcher che crea un nodo è null: nessun nodo creato, come un Annulla dell'utente.</summary>
public sealed class NullHabitBuilderLauncher : IHabitBuilderLauncher
{
    public Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default) =>
        Task.FromResult<Guid?>(null);
}
