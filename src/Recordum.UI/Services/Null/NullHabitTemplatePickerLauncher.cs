using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.UI.Services.Null;

/// <summary>Il picker dei template resta con la sua vista al di là di F1 (F5). "Annullato" per un
/// picker è null: nessun template scelto, come se l'utente avesse premuto Esc.</summary>
public sealed class NullHabitTemplatePickerLauncher : IHabitTemplatePickerLauncher
{
    public Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default) =>
        Task.FromResult<Guid?>(null);
}
