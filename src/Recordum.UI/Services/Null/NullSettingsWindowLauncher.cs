using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Settings;

namespace Recordum.UI.Services.Null;

/// <summary>La finestra Impostazioni resta con la sua vista al di là di F1. Un "mostra" senza vista
/// da mostrare è un no-op silenzioso, non un'eccezione: il ribbon la chiama a prescindere.</summary>
public sealed class NullSettingsWindowLauncher : ISettingsWindowLauncher
{
    public Task ShowAsync(SettingsCategoryId? initialCategory = null) => Task.CompletedTask;
}
